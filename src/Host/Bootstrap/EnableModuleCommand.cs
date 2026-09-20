using Access.Application;
using Contracts;

namespace Host.Bootstrap;

/// <summary>`dotnet Host.dll enable-tenant-module --tenant-id N --module crm` — gives a bootstrapped tenant a business
/// module's roles by copying the module's current capability template into tenant-local rows. Operator-only
/// (never HTTP), gated by `Bootstrap:Enabled` like the tenant-administrator bootstrap. Idempotent: re-running it
/// for an enabled tenant exits 0 and changes nothing — including when the template has since moved to a newer
/// version (there is no reconciler; see `EnableTenantModuleHandler`).</summary>
public static class EnableModuleCommand
{
    public const string Name = "enable-tenant-module";

    public const int Success = 0;
    public const int NotPermitted = BootstrapCommand.NotPermitted;
    public const int TenantNotBootstrapped = 4;
    public const int TemplateKeyConflict = 5;
    public const int BadArguments = BootstrapCommand.BadArguments;

    public static bool IsRequested(string[] args) => args.Length > 0 && args[0] == Name;

    public static async Task<int> RunAsync(
        IServiceProvider services,
        IConfiguration configuration,
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("Bootstrap:Enabled", false))
        {
            await error.WriteLineAsync("Refused: set Bootstrap:Enabled=true (environment variable Bootstrap__Enabled=true) to run this command.");
            return NotPermitted;
        }

        var options = CommandOptions.Parse(args.Skip(1).ToArray());
        if (!long.TryParse(options.GetValueOrDefault("tenant-id"), out var tenantId) || tenantId <= 0
            || string.IsNullOrWhiteSpace(options.GetValueOrDefault("module")))
        {
            await error.WriteLineAsync($"Usage: {Name} --tenant-id <positive number> --module <module key>");
            return BadArguments;
        }

        await using var scope = services.CreateAsyncScope();
        return await EnableAsync(scope.ServiceProvider, new TenantId(tenantId), options["module"], output, error, cancellationToken);
    }

    /// <summary>Shared with `bootstrap-tenant-admin --modules`.</summary>
    public static async Task<int> EnableAsync(
        IServiceProvider scopedServices, TenantId tenantId, string moduleKey, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var handler = scopedServices.GetRequiredService<EnableTenantModuleHandler>();
        var platformIssuer = scopedServices.GetRequiredService<Access.Application.Authentication.SessionOptions>().PlatformIssuer;
        var actor = new PrincipalRef(platformIssuer, "operator:" + Name);

        var result = await handler.HandleAsync(new EnableTenantModuleCommand(tenantId, moduleKey, actor, Guid.NewGuid()), cancellationToken);

        switch (result.Status)
        {
            case EnableTenantModuleStatus.Enabled:
                await output.WriteLineAsync(
                    $"Module '{moduleKey}' enabled for tenant {tenantId.Value} (template v{result.EnabledVersion}); "
                    + $"{result.GrantedAssignments} administrator role assignment(s) created.");
                return Success;

            case EnableTenantModuleStatus.AlreadyEnabled:
                var newer = result.LatestVersion > result.EnabledVersion
                    ? $" A newer template (v{result.LatestVersion}) exists; existing tenants are never upgraded automatically."
                    : string.Empty;
                await output.WriteLineAsync($"Module '{moduleKey}' is already enabled for tenant {tenantId.Value} (template v{result.EnabledVersion}); nothing changed.{newer}");
                return Success;

            case EnableTenantModuleStatus.TenantNotBootstrapped:
                await error.WriteLineAsync($"Refused: tenant {tenantId.Value} is not bootstrapped; run bootstrap-tenant-admin first.");
                return TenantNotBootstrapped;

            case EnableTenantModuleStatus.TemplateKeyConflict:
                await error.WriteLineAsync($"Refused: {result.Detail}");
                return TemplateKeyConflict;

            default:
                await error.WriteLineAsync($"Unknown module '{moduleKey}'.");
                return BadArguments;
        }
    }
}
