using System.Diagnostics.Metrics;
using System.Net;
using Host.Tests.Fixtures;
using Xunit;
using static Host.Tests.Authentication.LifecycleTestSupport;

namespace Host.Tests.Authentication;

/// <summary>`POST /tenants/{id}/invitations` and `/auth/invitations/{validate,accept}` end to end against the
/// real pipeline and a real PostgreSQL. Links and tokens are read from the Development mailbox.</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class InvitationEndpointsTests : IClassFixture<AuthApiFixture>
{
    private readonly AuthApiFixture _fixture;

    public InvitationEndpointsTests(AuthApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_tenant_administrator_invites_and_the_invitee_accepts_a_new_account_and_is_signed_in()
    {
        var tenant = AuthApiFixture.NewTenantId();
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var (_, adminToken) = await InviterAsync(_fixture, client, tenant);
        var email = AuthApiFixture.NewEmail();

        var invite = await Invite(client, tenant, adminToken, email, "Invited Person", "en");

        Assert.Equal(HttpStatusCode.Accepted, invite.StatusCode);
        var mail = await MailAsync(client, email, "invite");
        Assert.StartsWith($"{AllowedOrigin}/accept-invite#token=", mail.Link);
        Assert.DoesNotContain('?', mail.Link); // the token rides in the fragment: it never reaches servers, proxies or Referer

        var validate = await Validate(client, mail.Token);
        Assert.Equal(HttpStatusCode.OK, validate.StatusCode);
        var preview = await Json(validate);
        Assert.Equal("u***@example.test", preview.GetProperty("email").GetString());
        Assert.Equal(tenant, preview.GetProperty("tenantId").GetInt64());
        Assert.False(preview.GetProperty("accountHasCredential").GetBoolean());

        var accept = await Accept(client, mail.Token, NewPassword);

        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        Assert.Equal("no-store", accept.Headers.CacheControl?.ToString());
        Assert.Contains("httponly", SetCookieHeader(accept)!, StringComparison.OrdinalIgnoreCase);
        var session = await Json(accept);
        Assert.Equal("authenticated", session.GetProperty("status").GetString());
        Assert.Equal(tenant, session.GetProperty("activeTenant").GetProperty("tenantId").GetInt64());
        Assert.Equal(email, session.GetProperty("account").GetProperty("email").GetString());
        Assert.Equal("Invited Person", session.GetProperty("account").GetProperty("displayName").GetString());

        var me = await client.SendAsync(Get("/auth/me", session.GetProperty("accessToken").GetString()));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.False((await Json(me)).GetProperty("capabilities").GetProperty("canInviteMembers").GetBoolean()); // an invitee is a member, not an administrator

        Assert.Equal(HttpStatusCode.OK, (await Login(client, email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task An_existing_account_must_prove_its_password_and_a_wrong_one_does_not_burn_the_invitation()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var existing = await _fixture.SeedUserAsync(); // has a credential, belongs to no tenant yet
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var (_, adminToken) = await InviterAsync(_fixture, client, tenant);
        await Invite(client, tenant, adminToken, existing.Email);
        var mail = await MailAsync(client, existing.Email, "invite");

        Assert.True((await Json(await Validate(client, mail.Token))).GetProperty("accountHasCredential").GetBoolean());

        var wrong = await Accept(client, mail.Token, "Definitely-Not-The-Password-1");
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal("invalid_credentials", await ProblemType(wrong));

        var right = await Accept(client, mail.Token, AuthApiFixture.Password);
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.Equal(tenant, (await Json(right)).GetProperty("activeTenant").GetProperty("tenantId").GetInt64());
    }

    [Fact]
    public async Task A_used_an_expired_and_a_made_up_token_are_all_answered_identically()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var clock = new AdvanceableTimeProvider();
        using var host = await StartWithClockAsync(_fixture, clock);
        using var client = host.CreateClient();
        var (_, adminToken) = await InviterAsync(_fixture, client, tenant);

        var usedEmail = AuthApiFixture.NewEmail();
        await Invite(client, tenant, adminToken, usedEmail);
        var used = await MailAsync(client, usedEmail, "invite");
        Assert.Equal(HttpStatusCode.OK, (await Accept(client, used.Token, NewPassword)).StatusCode);

        var expiredEmail = AuthApiFixture.NewEmail();
        await Invite(client, tenant, adminToken, expiredEmail);
        var expired = await MailAsync(client, expiredEmail, "invite");
        clock.Advance(TimeSpan.FromDays(8)); // the default lifetime is 7 days

        var wrongSecret = used.Token[..used.Token.IndexOf('.')] + ".AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var bodies = new List<string>();
        foreach (var token in new[] { used.Token, expired.Token, wrongSecret, "garbage", "" })
        {
            var validate = await Validate(client, token);
            Assert.Equal(HttpStatusCode.BadRequest, validate.StatusCode);
            Assert.Equal("invalid_or_expired_token", await ProblemType(validate));
            bodies.Add(await StableBody(validate));
        }

        var replay = await Accept(client, used.Token, NewPassword);
        var late = await Accept(client, expired.Token, NewPassword);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);
        bodies.Add(await StableBody(replay));
        bodies.Add(await StableBody(late));

        Assert.Single(bodies.Distinct());
    }

    [Fact]
    public async Task A_weak_password_is_rejected_and_the_invitation_stays_usable()
    {
        var tenant = AuthApiFixture.NewTenantId();
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var (_, adminToken) = await InviterAsync(_fixture, client, tenant);
        var email = AuthApiFixture.NewEmail();
        await Invite(client, tenant, adminToken, email);
        var mail = await MailAsync(client, email, "invite");

        var weak = await Accept(client, mail.Token, "short");

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal("password_policy_violation", await ProblemType(weak));
        Assert.Equal(HttpStatusCode.OK, (await Accept(client, mail.Token, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Only_a_permitted_member_can_invite_and_only_into_their_own_tenant()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var otherTenant = AuthApiFixture.NewTenantId();
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var (_, adminToken) = await InviterAsync(_fixture, client, tenant);
        var plainMember = await _fixture.SeedUserAsync(tenantIds: tenant); // member, but no invite grant
        var plainToken = await AccessTokenAsync(client, plainMember.Email, AuthApiFixture.Password);
        var target = AuthApiFixture.NewEmail();

        var noGrant = await Invite(client, tenant, plainToken, target);
        Assert.Equal(HttpStatusCode.Forbidden, noGrant.StatusCode);
        Assert.Equal("forbidden", await ProblemType(noGrant));

        var foreignTenant = await Invite(client, otherTenant, adminToken, target);
        Assert.Equal(HttpStatusCode.Forbidden, foreignTenant.StatusCode);
        Assert.Equal("tenant_not_permitted", await ProblemType(foreignTenant));

        var anonymous = await client.SendAsync(Post($"/tenants/{tenant}/invitations", new { email = target }));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var noHeader = await client.SendAsync(Post($"/tenants/{tenant}/invitations", new { email = target }, adminToken, csrfHeader: false));
        Assert.Equal(HttpStatusCode.Forbidden, noHeader.StatusCode);
        Assert.Equal("csrf_rejected", await ProblemType(noHeader));

        var invalid = await Invite(client, tenant, adminToken, "not-an-address");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        Assert.Empty(await MailboxAsync(client, target)); // none of the refused attempts sent anything
    }

    [Fact]
    public async Task Accepting_needs_the_csrf_header_and_a_listed_origin_but_previewing_does_not()
    {
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();

        var noHeader = await client.SendAsync(Post("/auth/invitations/accept", new { token = "x.y", password = NewPassword }, csrfHeader: false));
        Assert.Equal(HttpStatusCode.Forbidden, noHeader.StatusCode);
        Assert.Equal("csrf_rejected", await ProblemType(noHeader));

        var foreign = await client.SendAsync(Post("/auth/invitations/accept", new { token = "x.y", password = NewPassword }, origin: "https://evil.example"));
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);

        var allowed = await client.SendAsync(Post("/auth/invitations/accept", new { token = "x.y", password = NewPassword }, origin: AllowedOrigin));
        Assert.Equal(HttpStatusCode.BadRequest, allowed.StatusCode); // got past the guard, then the token itself was refused

        Assert.Equal(HttpStatusCode.BadRequest, (await Validate(client, "x.y")).StatusCode); // read-only: no CSRF header needed
    }

    [Fact]
    public async Task Inviting_again_replaces_the_earlier_link()
    {
        var tenant = AuthApiFixture.NewTenantId();
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var (_, adminToken) = await InviterAsync(_fixture, client, tenant);
        var email = AuthApiFixture.NewEmail();

        await Invite(client, tenant, adminToken, email);
        var first = await MailAsync(client, email, "invite");
        await Invite(client, tenant, adminToken, email);
        var second = await MailAsync(client, email, "invite");

        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal(HttpStatusCode.BadRequest, (await Validate(client, first.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Validate(client, second.Token)).StatusCode);
    }

    [Fact]
    public async Task Me_reports_whether_the_actor_may_invite_members()
    {
        var tenant = AuthApiFixture.NewTenantId();
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var (_, adminToken) = await InviterAsync(_fixture, client, tenant);
        var plain = await _fixture.SeedUserAsync(tenantIds: tenant);
        var plainToken = await AccessTokenAsync(client, plain.Email, AuthApiFixture.Password);

        var admin = await Json(await client.SendAsync(Get("/auth/me", adminToken)));
        var member = await Json(await client.SendAsync(Get("/auth/me", plainToken)));

        Assert.True(admin.GetProperty("capabilities").GetProperty("canInviteMembers").GetBoolean());
        Assert.False(member.GetProperty("capabilities").GetProperty("canInviteMembers").GetBoolean());
    }

    [Fact]
    public async Task Token_endpoints_are_rate_limited_per_client_with_retry_after()
    {
        using var host = await _fixture.StartHostAsync(new() { ["Authentication__RateLimiting__TokenPer15Minutes"] = "2" });
        using var client = host.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await Validate(client, "a.b")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Validate(client, "a.b")).StatusCode);
        var limited = await Validate(client, "a.b");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
        Assert.Equal("rate_limited", await ProblemType(limited));
    }

    [Fact]
    public async Task Refused_requests_are_counted_by_the_policy_that_refused_them()
    {
        using var host = await _fixture.StartHostAsync(new() { ["Authentication__RateLimiting__TokenPer15Minutes"] = "1" });
        using var client = host.CreateClient();
        var policies = new List<string?>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == "Fynovio.Auth" && instrument.Name == "auth.ratelimit.rejected")
                    l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
                if (tag.Key == "policy")
                    lock (policies)
                        policies.Add(tag.Value?.ToString());
        });
        listener.Start();

        await Validate(client, "a.b");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Validate(client, "a.b")).StatusCode);

        lock (policies)
            Assert.Contains("auth-token", policies);
    }
}
