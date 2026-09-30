using Access.Application;
using Access.Application.Authentication;
using Access.Domain.Identity;
using Access.Persistence;
using Collaboration.Application;
using Contracts;
using CRM.Application;
using CRM.Persistence;
using MasterData.Application;
using Microsoft.EntityFrameworkCore;
using TenantLifecycle.Application;

namespace Host.Authentication;

/// <summary>Bound from the "DevSeed" configuration section. Only ever honoured in the
/// Development environment (see <see cref="DevSeeder.RunAsync"/>); the password is a
/// dev-only value and must never be reused anywhere else.</summary>
public sealed class DevSeedOptions
{
    public bool Enabled { get; init; }
    public string? Password { get; init; }
}

/// <summary>Idempotent local-development data: four sign-in identities that exercise every
/// state the login flow can end in, plus the CRM roles and pipeline the Opportunity API needs
/// (<see cref="CrmDevSeed"/>) and the Collaboration calendar role. Production has NO seed and no default credential — the
/// one-time bootstrap command (Phase 2.5A slice S3) is the only production path.
/// Uses the real Access handlers, so it goes through the same RLS-bound runtime role and the
/// same password policy as any other account.</summary>
public static class DevSeeder
{
    public static readonly TenantId TenantOne = new(1);
    public static readonly TenantId TenantTwo = new(2);

    /// <summary>Tenant administrator of both tenants → the tenant-selection state.</summary>
    public const string AdminEmail = "admin@fynovio.local";

    /// <summary>Active member of tenant 1 only, no grants → auto-selected tenant, CRM and Collaboration calls are 403.</summary>
    public const string SingleTenantEmail = "single@fynovio.local";

    /// <summary>Active member of tenant 1 only, read-only CRM grants (`crm.opportunity.read`/`list`) →
    /// sees Opportunities but every mutation is denied and every available action is false.</summary>
    public const string ViewerEmail = "viewer@fynovio.local";

    /// <summary>Active member of tenant 1 only, the CRM sales-representative template role (works opportunities,
    /// cannot reassign) → an eligible assignee besides the administrator.</summary>
    public const string SalesRepEmail = "rep@fynovio.local";

    /// <summary>Account without any membership → the `no_membership` state.</summary>
    public const string NoMembershipEmail = "nomember@fynovio.local";

    public static async Task RunAsync(
        IServiceProvider services,
        IHostEnvironment environment,
        DevSeedOptions options,
        string platformIssuer,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
            return;

        // Double guard: the flag alone must never seed anything outside Development.
        if (!environment.IsDevelopment())
        {
            logger.LogWarning("DevSeed:Enabled is set but the environment is {Environment}; the seed is skipped.", environment.EnvironmentName);
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Password))
            throw new InvalidOperationException("DevSeed:Password is required when DevSeed:Enabled is true.");

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var provision = scope.ServiceProvider.GetRequiredService<ProvisionPasswordAccountHandler>();
        var bootstrap = scope.ServiceProvider.GetRequiredService<BootstrapTenantAccessHandler>();

        var seeded = 0;

        if (!await CredentialExistsAsync(context, AdminEmail, cancellationToken))
        {
            var admin = await provision.HandleAsync(
                new ProvisionPasswordAccountCommand(AdminEmail, "Dev Admin", options.Password, platformIssuer, TenantOne),
                cancellationToken);
            await EnsureActiveMembershipAsync(context, admin.AccountId, TenantTwo, cancellationToken);

            foreach (var tenant in new[] { TenantOne, TenantTwo })
            {
                if (!await IsBootstrappedAsync(context, tenant, cancellationToken))
                    await bootstrap.HandleAsync(new BootstrapTenantAccessCommand(tenant, admin.Principal, Guid.NewGuid()), cancellationToken);
            }

            seeded++;
        }

        if (!await CredentialExistsAsync(context, SingleTenantEmail, cancellationToken))
        {
            await provision.HandleAsync(
                new ProvisionPasswordAccountCommand(SingleTenantEmail, "Dev Single-Tenant User", options.Password, platformIssuer, TenantOne),
                cancellationToken);
            seeded++;
        }

        if (!await CredentialExistsAsync(context, ViewerEmail, cancellationToken))
        {
            await provision.HandleAsync(
                new ProvisionPasswordAccountCommand(ViewerEmail, "Dev CRM Viewer", options.Password, platformIssuer, TenantOne),
                cancellationToken);
            seeded++;
        }

        if (!await CredentialExistsAsync(context, SalesRepEmail, cancellationToken))
        {
            await provision.HandleAsync(
                new ProvisionPasswordAccountCommand(SalesRepEmail, "Dev Sales Representative", options.Password, platformIssuer, TenantOne),
                cancellationToken);
            seeded++;
        }

        if (!await CredentialExistsAsync(context, NoMembershipEmail, cancellationToken))
        {
            await provision.HandleAsync(
                new ProvisionPasswordAccountCommand(NoMembershipEmail, "Dev No-Membership User", options.Password, platformIssuer),
                cancellationToken);
            seeded++;
        }

        await SeedCrmAsync(scope.ServiceProvider, context, platformIssuer, logger, cancellationToken);
        await SeedCollaborationAsync(scope.ServiceProvider, context, platformIssuer, logger, cancellationToken);
        await SeedTenantProfilesAsync(scope.ServiceProvider, cancellationToken);
        await SeedTenantLifecycleModuleAsync(scope.ServiceProvider, platformIssuer, logger, cancellationToken);
        await SeedPersonaRolesAsync(context, cancellationToken);

        logger.LogInformation("Dev seed applied ({Created} new accounts). Sign-in identities: {Admin}, {Single}, {Viewer}, {SalesRep}, {NoMembership}.",
            seeded, AdminEmail, SingleTenantEmail, ViewerEmail, SalesRepEmail, NoMembershipEmail);
    }

    private static async Task SeedCrmAsync(
        IServiceProvider services, AccessDbContext access, string platformIssuer, ILogger logger, CancellationToken cancellationToken)
    {
        var enableModule = services.GetRequiredService<EnableTenantModuleHandler>();
        var seedOperator = new PrincipalRef(platformIssuer, "operator:dev-seed");

        foreach (var tenant in new[] { TenantOne, TenantTwo })
        {
            // The production mechanism, not a dev shortcut: copies CRM's permission vocabulary.
            await EnableModuleAsync(enableModule, tenant, CrmModuleCapabilities.ModuleKey, "CRM", seedOperator, logger, cancellationToken);

            await CrmDevSeed.EnsurePipelineAsync(services.GetRequiredService<ProvisionPipelineHandler>(), tenant, cancellationToken);
            await PartyDevSeed.EnsurePartiesAsync(services.GetRequiredService<CreatePartyHandler>(), tenant, cancellationToken);
        }
    }

    /// <summary>Enables the calendar action vocabulary for both development tenants.</summary>
    private static async Task SeedCollaborationAsync(
        IServiceProvider services, AccessDbContext access, string platformIssuer, ILogger logger, CancellationToken cancellationToken)
    {
        var enableModule = services.GetRequiredService<EnableTenantModuleHandler>();
        var seedOperator = new PrincipalRef(platformIssuer, "operator:dev-seed");

        foreach (var tenant in new[] { TenantOne, TenantTwo })
            await EnableModuleAsync(enableModule, tenant, CollaborationModuleCapabilities.ModuleKey, "Collaboration", seedOperator, logger, cancellationToken);
    }

    /// <summary>The viewer and sales representative personas (tenant 1 only — an assignment needs an active membership
    /// there) get tenant-authored roles built from the module templates' permission sets, as an administrator would
    /// compose them; both keep a personal calendar. The single-tenant account deliberately stays without grants
    /// (its purpose is the 403 state).</summary>
    private static async Task SeedPersonaRolesAsync(AccessDbContext access, CancellationToken cancellationToken)
    {
        var adminAccountId = await AccountIdAsync(access, AdminEmail, cancellationToken);
        var viewerAccountId = await AccountIdAsync(access, ViewerEmail, cancellationToken);
        var salesRepAccountId = await AccountIdAsync(access, SalesRepEmail, cancellationToken);

        IReadOnlyList<PermissionSetTemplateItem> Items(ModuleCapabilityManifest manifest, params string[] setKeys) =>
            [.. manifest.PermissionSets.Where(set => setKeys.Length == 0 || setKeys.Contains(set.Key)).SelectMany(set => set.Items)];
        var calendar = Items(CollaborationModuleCapabilities.Manifest);
        var read = Items(CrmModuleCapabilities.Manifest, CrmModuleCapabilities.ReadSetKey);
        var write = Items(CrmModuleCapabilities.Manifest, CrmModuleCapabilities.WriteSetKey);

        await CrmDevSeed.EnsureTenantRoleAsync(access, TenantOne, "dev_crm_viewer", "CRM Viewer",
            [.. read, .. calendar], viewerAccountId, adminAccountId, cancellationToken);
        await CrmDevSeed.EnsureTenantRoleAsync(access, TenantOne, "dev_sales_representative", "Sales Representative",
            [.. read, .. write, .. calendar], salesRepAccountId, adminAccountId, cancellationToken);
    }

    private static async Task SeedTenantProfilesAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var provision = services.GetRequiredService<ProvisionTenantProfileHandler>();
        await provision.HandleAsync(new ProvisionTenantProfileCommand(TenantOne, "Fynovio Development 1", Guid.NewGuid()), cancellationToken);
        await provision.HandleAsync(new ProvisionTenantProfileCommand(TenantTwo, "Fynovio Development 2", Guid.NewGuid()), cancellationToken);
    }

    private static async Task SeedTenantLifecycleModuleAsync(IServiceProvider services, string platformIssuer, ILogger logger, CancellationToken cancellationToken)
    {
        var enableModule = services.GetRequiredService<EnableTenantModuleHandler>();
        var seedOperator = new PrincipalRef(platformIssuer, "operator:dev-seed");
        foreach (var tenant in new[] { TenantOne, TenantTwo })
            await EnableModuleAsync(enableModule, tenant, TenantLifecycleModuleCapabilities.ModuleKey, "Tenant Lifecycle", seedOperator, logger, cancellationToken);
    }

    private static async Task EnableModuleAsync(
        EnableTenantModuleHandler handler, TenantId tenant, string moduleKey, string moduleName, PrincipalRef seedOperator, ILogger logger, CancellationToken cancellationToken)
    {
        var enabled = await handler.HandleAsync(new EnableTenantModuleCommand(tenant, moduleKey, seedOperator, Guid.NewGuid()), cancellationToken);
        if (enabled.Status == EnableTenantModuleStatus.TemplateKeyConflict)
        {
            logger.LogWarning(
                "Tenant {Tenant} already has {Module} roles from an older dev seed, so the {Module} template was not applied. Reset the dev database to pick it up. {Detail}",
                tenant.Value, moduleName, moduleName, enabled.Detail);
        }
    }

    private static Task<long> AccountIdAsync(AccessDbContext context, string email, CancellationToken cancellationToken)
    {
        var normalized = EmailNormalizer.Normalize(email);
        return context.AccountCredentials
            .Where(c => c.LoginEmailNormalized == normalized)
            .Select(c => c.AccountId)
            .SingleAsync(cancellationToken);
    }

    // accounts/credentials are platform-global (no RLS), so this needs no tenant context.
    private static Task<bool> CredentialExistsAsync(AccessDbContext context, string email, CancellationToken cancellationToken)
    {
        var normalized = EmailNormalizer.Normalize(email);
        return context.AccountCredentials.AnyAsync(c => c.LoginEmailNormalized == normalized, cancellationToken);
    }

    // tenant_access_state and tenant_memberships are RLS-protected: read/write inside a transaction
    // with the tenant GUC set, exactly like the production handlers do.
    private static async Task<bool> IsBootstrappedAsync(AccessDbContext context, TenantId tenantId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(tenantId, cancellationToken);
        return await context.TenantAccessStates.AnyAsync(s => s.TenantId == tenantId, cancellationToken);
    }

    private static async Task EnsureActiveMembershipAsync(AccessDbContext context, long accountId, TenantId tenantId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(tenantId, cancellationToken);

        if (await context.TenantMemberships.AnyAsync(m => m.AccountId == accountId && m.TenantId == tenantId, cancellationToken))
            return;

        var membership = TenantMembership.Invite(tenantId, accountId);
        membership.Activate();
        context.TenantMemberships.Add(membership);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
