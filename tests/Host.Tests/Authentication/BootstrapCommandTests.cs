using System.Net;
using System.Text.RegularExpressions;
using Host.Bootstrap;
using Host.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Host.Tests.Authentication.LifecycleTestSupport;

namespace Host.Tests.Authentication;

/// <summary>The production bootstrap operator command: refuses unless enabled, hands the operator a single-use
/// password-setup link (never a default credential), refuses a second run.</summary>
[Collection(HostIntegrationCollection.Name)]
public sealed class BootstrapCommandTests : IClassFixture<AuthApiFixture>
{
    private readonly AuthApiFixture _fixture;

    public BootstrapCommandTests(AuthApiFixture fixture) => _fixture = fixture;

    private static readonly Dictionary<string, string?> Enabled = new() { ["Bootstrap__Enabled"] = "true" };

    private static async Task<(int Code, string Output, string Error)> RunAsync(AuthApiHost host, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await BootstrapCommand.RunAsync(
            host.Services,
            host.Services.GetRequiredService<IConfiguration>(),
            [BootstrapCommand.Name, .. args],
            output,
            error);
        return (code, output.ToString(), error.ToString());
    }

    private static string TokenFrom(string output) =>
        Uri.UnescapeDataString(Regex.Match(output, "#token=(\\S+)").Groups[1].Value);

    [Fact]
    public void The_command_is_recognised_only_by_its_exact_name()
    {
        Assert.True(BootstrapCommand.IsRequested([BootstrapCommand.Name]));
        Assert.False(BootstrapCommand.IsRequested([]));
        Assert.False(BootstrapCommand.IsRequested(["bootstrap"]));
        Assert.False(BootstrapCommand.IsRequested(["--urls", BootstrapCommand.Name]));
    }

    [Fact]
    public async Task It_refuses_to_run_unless_bootstrap_is_explicitly_enabled()
    {
        var tenant = AuthApiFixture.NewTenantId();
        using var host = await _fixture.StartHostAsync();

        var (code, output, error) = await RunAsync(host, "--tenant-id", tenant.ToString(), "--email", AuthApiFixture.NewEmail(), "--display-name", "Ops Admin");

        Assert.Equal(BootstrapCommand.NotPermitted, code);
        Assert.Empty(output);
        Assert.Contains("Bootstrap:Enabled", error);
    }

    [Fact]
    public async Task It_creates_the_administrator_and_prints_a_single_use_setup_link_that_lets_them_in()
    {
        var tenant = AuthApiFixture.NewTenantId();
        var email = AuthApiFixture.NewEmail();
        var logs = new CapturingLoggerProvider();
        using var host = await _fixture.StartHostAsync(Enabled, logProvider: logs);
        using var client = host.CreateClient();

        var (code, output, error) = await RunAsync(host, "--tenant-id", tenant.ToString(), $"--email={email}", "--display-name", "Ops Admin");

        Assert.Equal(BootstrapCommand.Success, code);
        Assert.Empty(error);
        Assert.Contains($"{AllowedOrigin}/reset-password#token=", output);
        var token = TokenFrom(output);

        // No credential exists yet, so nothing can sign in until the operator's link is used.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(client, email, NewPassword)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await Reset(client, token, NewPassword)).StatusCode);

        var login = await Login(client, email, NewPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await Json(login);
        Assert.Equal("authenticated", session.GetProperty("status").GetString());
        Assert.Equal(tenant, session.GetProperty("activeTenant").GetProperty("tenantId").GetInt64());
        var me = await Json(await client.SendAsync(Get("/auth/me", session.GetProperty("accessToken").GetString())));
        Assert.True(me.GetProperty("capabilities").GetProperty("canInviteMembers").GetBoolean()); // a tenant administrator

        var replay = await Reset(client, token, "Replayed-Passw0rd-Entirely-5");
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);

        Assert.DoesNotContain(token, string.Join('\n', logs.Entries)); // the link goes to stdout, never to the logs
    }

    [Fact]
    public async Task A_second_run_for_the_same_tenant_is_refused()
    {
        var tenant = AuthApiFixture.NewTenantId();
        using var host = await _fixture.StartHostAsync(Enabled);

        var first = await RunAsync(host, "--tenant-id", tenant.ToString(), "--email", AuthApiFixture.NewEmail(), "--display-name", "First Admin");
        var second = await RunAsync(host, "--tenant-id", tenant.ToString(), "--email", AuthApiFixture.NewEmail(), "--display-name", "Second Admin");

        Assert.Equal(BootstrapCommand.Success, first.Code);
        Assert.Equal(BootstrapCommand.AlreadyBootstrapped, second.Code);
        Assert.Empty(second.Output);
    }

    [Theory]
    [InlineData("--email", "a@example.test", "--display-name", "Name")]
    [InlineData("--tenant-id", "0", "--email", "a@example.test", "--display-name", "Name")]
    [InlineData("--tenant-id", "abc", "--email", "a@example.test", "--display-name", "Name")]
    [InlineData("--tenant-id", "5", "--display-name", "Name")]
    [InlineData("--tenant-id", "5", "--email", "a@example.test")]
    public async Task Missing_or_malformed_arguments_print_usage_and_change_nothing(params string[] args)
    {
        using var host = await _fixture.StartHostAsync(Enabled);

        var (code, output, error) = await RunAsync(host, args);

        Assert.Equal(BootstrapCommand.BadArguments, code);
        Assert.Empty(output);
        Assert.Contains("Usage", error);
    }

    [Fact]
    public async Task An_invalid_email_is_refused_with_the_bad_arguments_code()
    {
        using var host = await _fixture.StartHostAsync(Enabled);

        var (code, output, _) = await RunAsync(host, "--tenant-id", AuthApiFixture.NewTenantId().ToString(), "--email", "not-an-address", "--display-name", "Name");

        Assert.Equal(BootstrapCommand.BadArguments, code);
        Assert.Empty(output);
    }
}
