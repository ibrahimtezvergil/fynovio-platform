using Access.Application;
using Access.Application.Authentication;
using Access.Domain.Authentication;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Access.Tests.Integration.Authentication;

/// <summary>Creating and previewing invitations (`identity.membership.invite`).</summary>
public sealed class InvitationCreationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public InvitationCreationTests(PostgresFixture fixture) => _fixture = fixture;

    private static async Task<AccountToken?> OutstandingInviteAsync(PostgresFixture fixture, long tenant, string email)
    {
        await using var admin = fixture.CreateAdminContext();
        return await admin.AccountTokens.AsNoTracking()
            .Where(t => t.Purpose == AccountTokenPurpose.Invite && t.TenantId == tenant && t.EmailNormalized == email && t.RevokedAt == null && t.ConsumedAt == null)
            .SingleOrDefaultAsync();
    }

    [Fact]
    public void The_invite_action_is_part_of_the_catalog()
    {
        var descriptor = Assert.Single(AccessActionCatalog.All, d => d.ActionKey == CreateInvitationHandler.ActionKeyValue);
        Assert.Equal("identity.membership.invite", descriptor.ActionKey);
    }

    [Fact]
    public async Task A_tenant_administrator_can_invite_and_the_mail_carries_a_redeemable_token()
    {
        var tenant = AuthTestSetup.NewTenant();
        var (_, admin) = await AuthTestSetup.SeedTenantAdminAsync(_fixture, tenant);
        var mail = new FakeEmailSender();
        var time = new TestTimeProvider();
        var email = AuthTestSetup.NewEmail().ToUpperInvariant(); // normalisation must not matter to the invitee
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);

        var result = await AuthTestSetup.Invitations(context, mail, time)
            .HandleAsync(new CreateInvitationCommand(AuthTestSetup.Actor(tenant, admin), email, "New Person", "tr"));

        Assert.Equal(CreateInvitationStatus.Accepted, result.Status);
        var normalized = EmailNormalizer.Normalize(email);
        var stored = await OutstandingInviteAsync(_fixture, tenant.Value, normalized);
        Assert.NotNull(stored);
        Assert.Equal("New Person", stored.DisplayName);
        Assert.Equal(time.GetUtcNow().AddDays(7), stored.ExpiresAt);
        Assert.NotNull(stored.CreatedByAccountId);

        var message = Assert.Single(mail.Messages);
        Assert.Equal(EmailMessage.InviteTemplate, message.TemplateId);
        Assert.Equal(normalized, message.To);
        Assert.Equal("tr", message.Locale);
        Assert.Equal(tenant.Value.ToString(), message.Values["tenantId"]);

        // The mailed token is the one whose hash is stored — and the secret itself is not.
        await using var verify = await AuthTestSetup.RuntimeContextAsync(_fixture);
        Assert.NotNull(await AuthTestSetup.Tokens(verify).FindOutstandingAsync(message.Values["token"], [AccountTokenPurpose.Invite], time.GetUtcNow(), default));
        var secret = message.Values["token"][(message.Values["token"].IndexOf('.') + 1)..];
        Assert.NotEqual(secret, stored.TokenHash);
    }

    [Fact]
    public async Task Inviting_writes_tenant_evidence_and_outbox_without_the_address_or_the_token()
    {
        var tenant = AuthTestSetup.NewTenant();
        var (_, admin) = await AuthTestSetup.SeedTenantAdminAsync(_fixture, tenant);
        var mail = new FakeEmailSender();
        var email = AuthTestSetup.NewEmail();
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);

        await AuthTestSetup.Invitations(context, mail, new TestTimeProvider())
            .HandleAsync(new CreateInvitationCommand(AuthTestSetup.Actor(tenant, admin), email));

        await using var verify = _fixture.CreateAdminContext();
        var evidence = await verify.EvidenceRecords.SingleAsync(e => e.TenantId == tenant && e.Action == "Tenant.InviteMember");
        var outbox = await verify.OutboxMessages.SingleAsync(m => m.TenantId == tenant && m.EventType == "enterprise.access.membership.invited.v1");
        var token = mail.Messages.Single().Values["token"];
        Assert.All(new[] { evidence.Detail, outbox.Payload, outbox.Subject }, text =>
        {
            Assert.DoesNotContain(email, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(token, text);
            Assert.DoesNotContain(token[(token.IndexOf('.') + 1)..], text);
        });

        var auditEvent = await verify.AuthEvents.SingleAsync(e => e.EventType == "invite_created" && e.TenantId == tenant.Value);
        Assert.Equal("success", auditEvent.Outcome);
    }

    [Fact]
    public async Task A_member_without_the_grant_is_forbidden_and_nothing_is_created_or_sent()
    {
        var tenant = AuthTestSetup.NewTenant();
        await AuthTestSetup.SeedTenantAdminAsync(_fixture, tenant); // tenant exists and is bootstrapped
        var (_, plainMember) = await AuthTestSetup.SeedAccountAsync(_fixture, AuthTestSetup.NewEmail(), tenant: tenant);
        var mail = new FakeEmailSender();
        var email = AuthTestSetup.NewEmail();
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);

        var result = await AuthTestSetup.Invitations(context, mail, new TestTimeProvider())
            .HandleAsync(new CreateInvitationCommand(AuthTestSetup.Actor(tenant, plainMember), email));

        Assert.Equal(CreateInvitationStatus.Forbidden, result.Status);
        Assert.Null(await OutstandingInviteAsync(_fixture, tenant.Value, EmailNormalizer.Normalize(email)));
        Assert.Empty(mail.Messages);
        await using var verify = _fixture.CreateAdminContext();
        Assert.Equal("forbidden", (await verify.AuthEvents.SingleAsync(e => e.EventType == "invite_created" && e.TenantId == tenant.Value && e.Outcome == "forbidden")).Outcome);
    }

    [Fact]
    public async Task An_administrator_of_another_tenant_cannot_invite_into_this_one()
    {
        var home = AuthTestSetup.NewTenant();
        var foreign = AuthTestSetup.NewTenant();
        await AuthTestSetup.SeedTenantAdminAsync(_fixture, foreign);
        var (_, homeAdmin) = await AuthTestSetup.SeedTenantAdminAsync(_fixture, home);
        var mail = new FakeEmailSender();
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);

        // Claims the foreign tenant while only being a member/admin of `home`.
        var result = await AuthTestSetup.Invitations(context, mail, new TestTimeProvider())
            .HandleAsync(new CreateInvitationCommand(AuthTestSetup.Actor(foreign, homeAdmin), AuthTestSetup.NewEmail()));

        Assert.Equal(CreateInvitationStatus.Forbidden, result.Status);
        Assert.Empty(mail.Messages);
    }

    [Fact]
    public async Task A_tenant_that_was_never_bootstrapped_denies_everyone()
    {
        var tenant = AuthTestSetup.NewTenant();
        var (_, member) = await AuthTestSetup.SeedAccountAsync(_fixture, AuthTestSetup.NewEmail(), tenant: tenant);
        var mail = new FakeEmailSender();
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);

        var result = await AuthTestSetup.Invitations(context, mail, new TestTimeProvider())
            .HandleAsync(new CreateInvitationCommand(AuthTestSetup.Actor(tenant, member), AuthTestSetup.NewEmail()));

        Assert.Equal(CreateInvitationStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task Authorization_is_checked_before_the_input_is_looked_at()
    {
        var tenant = AuthTestSetup.NewTenant();
        await AuthTestSetup.SeedTenantAdminAsync(_fixture, tenant);
        var (_, plainMember) = await AuthTestSetup.SeedAccountAsync(_fixture, AuthTestSetup.NewEmail(), tenant: tenant);
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);

        var result = await AuthTestSetup.Invitations(context, new FakeEmailSender(), new TestTimeProvider())
            .HandleAsync(new CreateInvitationCommand(AuthTestSetup.Actor(tenant, plainMember), "not-an-email"));

        Assert.Equal(CreateInvitationStatus.Forbidden, result.Status); // not InvalidEmail: a denied caller learns nothing about the input
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-at-sign")]
    [InlineData("two@@example.com")]
    [InlineData("a b@example.com")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user@nodot")]
    public async Task An_authorised_caller_gets_InvalidEmail_for_impossible_addresses(string email)
    {
        var tenant = AuthTestSetup.NewTenant();
        var (_, admin) = await AuthTestSetup.SeedTenantAdminAsync(_fixture, tenant);
        var mail = new FakeEmailSender();
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);

        var result = await AuthTestSetup.Invitations(context, mail, new TestTimeProvider())
            .HandleAsync(new CreateInvitationCommand(AuthTestSetup.Actor(tenant, admin), email));

        Assert.Equal(CreateInvitationStatus.InvalidEmail, result.Status);
        Assert.Empty(mail.Messages);
    }

    [Fact]
    public async Task Re_inviting_revokes_the_previous_invitation_so_only_the_newest_is_redeemable()
    {
        var tenant = AuthTestSetup.NewTenant();
        var (_, admin) = await AuthTestSetup.SeedTenantAdminAsync(_fixture, tenant);
        var mail = new FakeEmailSender();
        var time = new TestTimeProvider();
        var email = AuthTestSetup.NewEmail();

        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);
        var handler = AuthTestSetup.Invitations(context, mail, time);
        await handler.HandleAsync(new CreateInvitationCommand(AuthTestSetup.Actor(tenant, admin), email));
        await handler.HandleAsync(new CreateInvitationCommand(AuthTestSetup.Actor(tenant, admin), email));

        var (first, second) = (mail.Messages[0].Values["token"], mail.Messages[1].Values["token"]);
        await using var verify = await AuthTestSetup.RuntimeContextAsync(_fixture);
        var validate = new ValidateInvitationHandler(verify, AuthTestSetup.Tokens(verify), time);
        Assert.Equal(ValidateInvitationStatus.InvalidOrExpiredToken, (await validate.HandleAsync(first)).Status);
        Assert.Equal(ValidateInvitationStatus.Valid, (await validate.HandleAsync(second)).Status);
        Assert.NotNull(await OutstandingInviteAsync(_fixture, tenant.Value, EmailNormalizer.Normalize(email)));
    }

    [Fact]
    public async Task The_result_does_not_reveal_whether_the_address_already_has_an_account_or_membership()
    {
        var tenant = AuthTestSetup.NewTenant();
        var (_, admin) = await AuthTestSetup.SeedTenantAdminAsync(_fixture, tenant);
        var existingEmail = AuthTestSetup.NewEmail();
        await AuthTestSetup.SeedAccountAsync(_fixture, existingEmail, tenant: tenant); // already an Active member
        var mail = new FakeEmailSender();
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);
        var handler = AuthTestSetup.Invitations(context, mail, new TestTimeProvider());

        var known = await handler.HandleAsync(new CreateInvitationCommand(AuthTestSetup.Actor(tenant, admin), existingEmail));
        var unknown = await handler.HandleAsync(new CreateInvitationCommand(AuthTestSetup.Actor(tenant, admin), AuthTestSetup.NewEmail()));

        Assert.Equal(known, unknown);
        Assert.Equal(2, mail.Messages.Count); // one mail per accepted request, either way
    }

    // ---- preview ---------------------------------------------------------------------------

    [Fact]
    public async Task Preview_shows_a_masked_address_the_tenant_and_whether_a_credential_exists_without_consuming()
    {
        var tenant = AuthTestSetup.NewTenant();
        var (_, admin) = await AuthTestSetup.SeedTenantAdminAsync(_fixture, tenant);
        var mail = new FakeEmailSender();
        var time = new TestTimeProvider();
        var newcomer = AuthTestSetup.NewEmail();
        var returning = AuthTestSetup.NewEmail();
        await AuthTestSetup.SeedAccountAsync(_fixture, returning); // has a credential, no membership yet
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);
        var invite = AuthTestSetup.Invitations(context, mail, time);
        await invite.HandleAsync(new CreateInvitationCommand(AuthTestSetup.Actor(tenant, admin), newcomer, "Nova"));
        await invite.HandleAsync(new CreateInvitationCommand(AuthTestSetup.Actor(tenant, admin), returning));

        await using var verify = await AuthTestSetup.RuntimeContextAsync(_fixture);
        var validate = new ValidateInvitationHandler(verify, AuthTestSetup.Tokens(verify), time);
        var forNewcomer = await validate.HandleAsync(mail.TokenFor(newcomer));
        var again = await validate.HandleAsync(mail.TokenFor(newcomer)); // a preview can be repeated
        var forReturning = await validate.HandleAsync(mail.TokenFor(returning));

        Assert.Equal(ValidateInvitationStatus.Valid, forNewcomer.Status);
        Assert.Equal(forNewcomer, again);
        Assert.Equal(tenant.Value, forNewcomer.TenantId);
        Assert.Equal("Nova", forNewcomer.DisplayName);
        Assert.False(forNewcomer.AccountHasCredential);
        Assert.True(forReturning.AccountHasCredential);
        Assert.Equal(EmailNormalizer.Normalize(newcomer)[0] + "***@example.com", forNewcomer.MaskedEmail);
        Assert.DoesNotContain(EmailNormalizer.Normalize(newcomer), forNewcomer.MaskedEmail);
        Assert.Equal(time.GetUtcNow().AddDays(7), forNewcomer.ExpiresAt);
        Assert.Null((await AuthTestSetup.LoadTokenAsync(_fixture, mail.TokenFor(newcomer))).ConsumedAt);
    }

    [Fact]
    public async Task Every_way_a_preview_can_be_invalid_produces_the_same_result()
    {
        var tenant = AuthTestSetup.NewTenant();
        var (adminId, admin) = await AuthTestSetup.SeedTenantAdminAsync(_fixture, tenant);
        var mail = new FakeEmailSender();
        var time = new TestTimeProvider();
        await using var context = await AuthTestSetup.RuntimeContextAsync(_fixture);
        var invite = AuthTestSetup.Invitations(context, mail, time);

        string Mail(string email) { invite.HandleAsync(new CreateInvitationCommand(AuthTestSetup.Actor(tenant, admin), email)).GetAwaiter().GetResult(); return mail.TokenFor(email); }

        var valid = Mail(AuthTestSetup.NewEmail());
        var consumed = Mail(AuthTestSetup.NewEmail());
        var revoked = Mail(AuthTestSetup.NewEmail());
        Assert.True(AccountTokenService.TryParse(consumed, out var consumedId, out _));
        Assert.True(AccountTokenService.TryParse(revoked, out var revokedId, out _));
        Assert.True(AccountTokenService.TryParse(valid, out var validId, out var validSecret));
        await using (var admins = _fixture.CreateAdminContext())
        {
            await admins.AccountTokens.Where(t => t.Id == consumedId).ExecuteUpdateAsync(s => s.SetProperty(t => t.ConsumedAt, time.GetUtcNow()));
            await admins.AccountTokens.Where(t => t.Id == revokedId).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, time.GetUtcNow()));
        }
        var resetToken = await AuthTestSetup.InsertTokenAsync(_fixture, h => AccountToken.CreatePasswordReset(adminId, h, time.GetUtcNow(), TimeSpan.FromMinutes(30)));

        await using var verify = await AuthTestSetup.RuntimeContextAsync(_fixture);
        var validate = new ValidateInvitationHandler(verify, AuthTestSetup.Tokens(verify), time);
        var expectedInvalid = new ValidateInvitationResult(ValidateInvitationStatus.InvalidOrExpiredToken);

        foreach (var raw in new[] { consumed, revoked, resetToken, $"{validId:D}.{validSecret}x", $"{Guid.NewGuid():D}.{validSecret}", "garbage", "", null })
            Assert.Equal(expectedInvalid, await validate.HandleAsync(raw));

        time.Advance(TimeSpan.FromDays(8)); // expiry
        Assert.Equal(expectedInvalid, await validate.HandleAsync(valid));
    }
}
