using Access.Application;
using Access.Application.Authentication;
using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Host.Authentication;

/// <summary>Bound from the "DevSeed" configuration section. Only ever honoured in the
/// Development environment (see <see cref="DevSeeder.RunAsync"/>); the password is a
/// dev-only value and must never be reused anywhere else.</summary>
public sealed class DevSeedOptions
{
    public bool Enabled { get; init; }
    public string? Password { get; init; }
}

/// <summary>Idempotent local-development data: three sign-in identities that exercise every
/// state the login flow can end in. Production has NO seed and no default credential — the
/// one-time bootstrap command (Phase 2.5A slice S3) is the only production path.
/// Uses the real Access handlers, so it goes through the same RLS-bound runtime role and the
/// same password policy as any other account.</summary>
public static class DevSeeder
{
    public static readonly TenantId TenantOne = new(1);
    public static readonly TenantId TenantTwo = new(2);

    /// <summary>Tenant administrator of both tenants → the tenant-selection state.</summary>
    public const string AdminEmail = "admin@fynovio.local";

    /// <summary>Active member of tenant 1 only, no grants → auto-selected tenant, CRM calls are 403.</summary>
    public const string SingleTenantEmail = "single@fynovio.local";

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

        if (!await CredentialExistsAsync(context, NoMembershipEmail, cancellationToken))
        {
            await provision.HandleAsync(
                new ProvisionPasswordAccountCommand(NoMembershipEmail, "Dev No-Membership User", options.Password, platformIssuer),
                cancellationToken);
            seeded++;
        }

        logger.LogInformation("Dev seed applied ({Created} new accounts). Sign-in identities: {Admin}, {Single}, {NoMembership}.",
            seeded, AdminEmail, SingleTenantEmail, NoMembershipEmail);
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
