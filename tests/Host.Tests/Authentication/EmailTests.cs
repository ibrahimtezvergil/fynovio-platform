using System.Net;
using Access.Application.Authentication;
using Host.Email;
using Host.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Xunit;
using static Host.Tests.Authentication.LifecycleTestSupport;

namespace Host.Tests.Authentication;

/// <summary>What the e-mail layer promises: links built from configuration with the token in the fragment,
/// delivery off the request path, failures that change nothing observable, and nothing secret in logs.</summary>
public sealed class EmailRendererTests
{
    private static EmailMessage Message(string template, string locale = "en", string token = "id.secret-value") =>
        new("someone@example.test", template, locale, new Dictionary<string, string> { ["token"] = token });

    [Fact]
    public void The_link_comes_from_the_configured_base_url_with_the_token_in_the_fragment()
    {
        var invite = EmailRenderer.Render(Message(EmailMessage.InviteTemplate), "https://app.example.test/", "Fynovio");
        var reset = EmailRenderer.Render(Message(EmailMessage.PasswordResetTemplate), "https://app.example.test", "Fynovio");

        Assert.Equal("https://app.example.test/accept-invite#token=id.secret-value", invite.Link);
        Assert.Equal("https://app.example.test/reset-password#token=id.secret-value", reset.Link);
        Assert.DoesNotContain("?", invite.Link);
        Assert.Contains(invite.Link, invite.TextBody);
    }

    [Fact]
    public void The_token_is_url_escaped_and_the_link_is_html_encoded_in_the_html_body()
    {
        var rendered = EmailRenderer.Render(Message(EmailMessage.InviteTemplate, token: "id.a+b/c=&\"<x>"), "https://app.example.test/?a=1&b=2", "Fynovio");

        Assert.Contains("#token=id.a%2Bb%2Fc%3D%26%22%3Cx%3E", rendered.Link);
        Assert.DoesNotContain("<x>", rendered.HtmlBody);
        Assert.Contains("?a=1&amp;b=2", rendered.HtmlBody); // the configured base is encoded inside the href
    }

    [Fact]
    public void Turkish_is_used_for_tr_and_english_otherwise()
    {
        var turkish = EmailRenderer.Render(Message(EmailMessage.PasswordResetTemplate, "tr"), "https://app.example.test", "Fynovio");
        var english = EmailRenderer.Render(Message(EmailMessage.PasswordResetTemplate, "de"), "https://app.example.test", "Fynovio");

        Assert.Contains("şifre", turkish.Subject);
        Assert.Contains("password", english.Subject);
    }

    [Fact]
    public void A_missing_token_or_an_unknown_template_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => EmailRenderer.Render(new("a@example.test", "invite", "en", new Dictionary<string, string>()), "https://x.test", "F"));
        Assert.Throws<InvalidOperationException>(() => EmailRenderer.Render(Message("marketing"), "https://x.test", "F"));
    }

    [Fact]
    public void Formatting_a_rendered_message_never_prints_the_token()
    {
        var rendered = EmailRenderer.Render(Message(EmailMessage.InviteTemplate), "https://app.example.test", "Fynovio");

        Assert.DoesNotContain("secret-value", rendered.ToString());
        Assert.DoesNotContain("secret-value", Message(EmailMessage.InviteTemplate).ToString());
    }
}

[Collection(HostIntegrationCollection.Name)]
public sealed class EmailDeliveryTests : IClassFixture<AuthApiFixture>
{
    private readonly AuthApiFixture _fixture;

    public EmailDeliveryTests(AuthApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Durable_invitation_delivery_clears_the_protected_token_after_processing()
    {
        var tenant = AuthApiFixture.NewTenantId();
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        var (_, bearer) = await InviterAsync(_fixture, client, tenant);
        var email = AuthApiFixture.NewEmail();
        Assert.Equal(HttpStatusCode.Accepted, (await Invite(client, tenant, bearer, email)).StatusCode);
        var sent = await MailAsync(client, email, "invite");
        Assert.True(AccountTokenService.TryParse(sent.Token, out var invitationId, out _));

        var worker = host.Services.GetServices<IHostedService>().OfType<InvitationDeliveryService>().Single();
        await worker.DeliverPendingAsync(CancellationToken.None);

        await using var verify = AuthTestFixture.CreateAccessContext(_fixture.AdminConnectionString);
        var delivery = await verify.InvitationDeliveries.SingleAsync(row => row.InvitationId == invitationId);
        Assert.NotNull(delivery.DeliveredAt);
        Assert.Empty(delivery.ProtectedToken);
    }

    private sealed class ScriptedTransport(Func<RenderedEmail, CancellationToken, Task> send) : IEmailTransport
    {
        public List<RenderedEmail> Delivered { get; } = [];

        public async Task SendAsync(RenderedEmail email, CancellationToken cancellationToken)
        {
            await send(email, cancellationToken);
            lock (Delivered)
                Delivered.Add(email);
        }
    }

    private static Action<IServiceCollection> UseTransport(IEmailTransport transport) => services =>
    {
        services.RemoveAll<IEmailTransport>();
        services.AddSingleton(transport);
    };

    private static async Task<bool> EventuallyAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
            await Task.Delay(50);
        return condition();
    }

    [Fact]
    public async Task A_slow_mail_transport_cannot_slow_the_response_so_timing_does_not_reveal_which_addresses_exist()
    {
        var known = await _fixture.SeedUserAsync(tenantIds: AuthApiFixture.NewTenantId());
        var release = new TaskCompletionSource();
        var transport = new ScriptedTransport(async (_, _) => await release.Task);
        using var host = await _fixture.StartHostAsync(configureServices: UseTransport(transport));
        using var client = host.CreateClient();

        var request = Forgot(client, known.Email);
        var finished = await Task.WhenAny(request, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(request, finished); // returned while the transport was still blocked
        Assert.Equal(HttpStatusCode.Accepted, (await request).StatusCode);
        Assert.Empty(transport.Delivered);

        release.SetResult();
        Assert.True(await EventuallyAsync(() => transport.Delivered.Count == 1));
        Assert.Equal(known.Email, transport.Delivered[0].To);
        Assert.Equal("password_reset", transport.Delivered[0].TemplateId);
    }

    [Fact]
    public async Task A_failing_transport_changes_no_response_and_leaves_no_secret_in_the_logs()
    {
        var known = await _fixture.SeedUserAsync(tenantIds: AuthApiFixture.NewTenantId());
        var logs = new CapturingLoggerProvider();
        var attempted = new TaskCompletionSource<RenderedEmail>();
        var transport = new ScriptedTransport((email, _) =>
        {
            attempted.TrySetResult(email);
            throw new InvalidOperationException("smtp is down");
        });
        using var host = await _fixture.StartHostAsync(configureServices: UseTransport(transport), logProvider: logs);
        using var client = host.CreateClient();

        var response = await Forgot(client, known.Email);
        var email = await attempted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.True(await EventuallyAsync(() => logs.Entries.Any(e => e.Contains("could not be delivered"))));
        var everything = string.Join('\n', logs.Entries);
        Assert.DoesNotContain(email.Token, everything);
        Assert.DoesNotContain(email.Token.Split('.')[1], everything);
        Assert.DoesNotContain(email.Link, everything);
        Assert.DoesNotContain(known.Email, everything); // only the masked form is ever logged
    }

    [Fact]
    public async Task Without_an_smtp_provider_nothing_is_sent_and_the_log_says_so_without_the_link()
    {
        var known = await _fixture.SeedUserAsync(tenantIds: AuthApiFixture.NewTenantId());
        var logs = new CapturingLoggerProvider();
        using var host = await _fixture.StartHostAsync(logProvider: logs);
        using var client = host.CreateClient();

        await Forgot(client, known.Email);
        var mail = await MailAsync(client, known.Email, "password_reset");

        Assert.True(await EventuallyAsync(() => logs.Entries.Any(e => e.Contains("was not delivered"))));
        var everything = string.Join('\n', logs.Entries);
        Assert.DoesNotContain(mail.Token, everything);
        Assert.DoesNotContain(mail.Link, everything);
        Assert.DoesNotContain(known.Email, everything);
    }

    [Fact]
    public async Task Enabling_smtp_without_a_host_fails_at_start_up()
    {
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            using var host = await _fixture.StartHostAsync(new() { ["Email__Smtp__Enabled"] = "true" });
        });
    }

    [Fact]
    public async Task The_dev_mailbox_lists_filters_and_clears_in_development()
    {
        var user = await _fixture.SeedUserAsync(tenantIds: AuthApiFixture.NewTenantId());
        using var host = await _fixture.StartHostAsync();
        using var client = host.CreateClient();
        await Forgot(client, user.Email);

        Assert.Single(await MailboxAsync(client, user.Email));
        Assert.Empty(await MailboxAsync(client, AuthApiFixture.NewEmail()));

        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/dev/mailbox"))).StatusCode);
        Assert.Empty(await MailboxAsync(client, user.Email));
    }

    [Fact]
    public async Task The_dev_mailbox_does_not_exist_outside_development()
    {
        using var host = await _fixture.StartHostAsync(Production());
        using var client = host.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/dev/mailbox")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/dev/mailbox"))).StatusCode);
        Assert.Null(host.Services.GetService<DevMailbox>()); // not even registered, so nothing holds tokens in memory
    }

    [Fact]
    public async Task No_secret_reaches_the_logs_across_invite_accept_forgot_reset_and_change()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var logs = new CapturingLoggerProvider();
        using var host = await _fixture.StartHostAsync(logProvider: logs);
        using var client = host.CreateClient();
        var (_, adminToken) = await InviterAsync(_fixture, client, tenant);
        var email = AuthApiFixture.NewEmail();
        const string changedTo = "Distinctive-Changed-Passw0rd-11";

        await Invite(client, tenant, adminToken, email);
        var invite = await MailAsync(client, email, "invite");
        var accept = await Accept(client, invite.Token, NewPassword);
        var accessToken = (await Json(accept)).GetProperty("accessToken").GetString()!;
        await Change(client, accessToken, NewPassword, changedTo);
        await Forgot(client, email);
        var reset = await MailAsync(client, email, "password_reset");
        await Reset(client, reset.Token, "Distinctive-Reset-Passw0rd-12");

        var everything = string.Join('\n', logs.Entries);
        Assert.NotEmpty(logs.Entries);
        foreach (var secret in new[] { invite.Token, reset.Token, NewPassword, changedTo, "Distinctive-Reset-Passw0rd-12", accessToken, adminToken })
            Assert.DoesNotContain(secret, everything);
        Assert.DoesNotContain(invite.Token.Split('.')[1], everything);
        Assert.DoesNotContain(reset.Token.Split('.')[1], everything);
        Assert.DoesNotContain(email, everything);
    }
}
