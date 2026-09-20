using Access.Domain.Authentication;
using Access.Domain.Authorization;
using Access.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Access.Persistence;

/// <summary>Pilot exception (doc 08 §2): Identity and Access share one assembly and one
/// DbContext, but not one PostgreSQL schema — each keeps its own (`identity`, `access`),
/// matching docs/schema/identity-access-schema.md's title. Unlike CRM+Sales (one merged
/// schema, doc 17 §1), this merge is assembly-only: no `HasDefaultSchema` call here, every
/// entity configuration states its schema explicitly. Never referenced directly by another
/// module's project; cross-module access goes through Contracts-shaped values and the
/// outbox, not this DbContext (doc 08).</summary>
public sealed class AccessDbContext : DbContext
{
    public const string IdentitySchema = "identity";
    public const string AccessSchema = "access";

    public AccessDbContext(DbContextOptions<AccessDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<AccountCredential> AccountCredentials => Set<AccountCredential>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuthEvent> AuthEvents => Set<AuthEvent>();
    public DbSet<AccountToken> AccountTokens => Set<AccountToken>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<ActionRegistryEntry> Actions => Set<ActionRegistryEntry>();
    public DbSet<PermissionSet> PermissionSets => Set<PermissionSet>();
    public DbSet<PermissionSetItem> PermissionSetItems => Set<PermissionSetItem>();
    public DbSet<RolePermissionSet> RolePermissionSets => Set<RolePermissionSet>();
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();
    public DbSet<TenantAccessState> TenantAccessStates => Set<TenantAccessState>();
    public DbSet<Access.Outbox.OutboxMessage> OutboxMessages => Set<Access.Outbox.OutboxMessage>();
    public DbSet<Access.Evidence.EvidenceRecord> EvidenceRecords => Set<Access.Evidence.EvidenceRecord>();
    public DbSet<Access.Idempotency.IdempotencyRecord> IdempotencyRecords => Set<Access.Idempotency.IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccessDbContext).Assembly);
    }
}
