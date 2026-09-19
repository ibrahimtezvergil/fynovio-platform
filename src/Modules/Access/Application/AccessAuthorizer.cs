using Access.Domain.Authorization;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

/// <summary>Default-deny PDP. RBAC + tenant-wide scope + `owner` relation only —
/// Phase 1.5's frozen scope (round 3 §8 freeze #10). Fail-closed on every unresolved
/// input: unregistered action, unrecognized principal, no matching grant.</summary>
public sealed class AccessAuthorizer(
    AccessDbContext context,
    PrincipalResolver principalResolver,
    IActionCatalog actionCatalog) : IAuthorizer
{
    /// <summary>2026-09-20 PHASE_1_5_RUNTIME_RLS_PDP_DELTA: `access.role_assignments`,
    /// `access.role_permission_sets`, `access.permission_set_items`, and
    /// `access.tenant_access_state` are all RLS-protected. The claimed actor tenant
    /// establishes DB visibility for this evaluation; it is not itself an authorization
    /// outcome — every branch below still independently re-checks `TenantId`/`AccountId`/
    /// `ActionKey` before granting.</summary>
    public async Task<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        // Reentrant: Access's own command handlers (Grant/RevokeRoleAssignmentHandler) already
        // open a transaction and set tenant context for `request.Actor.TenantId` before calling
        // this authorizer on the same AccessDbContext instance — opening a second transaction on
        // an already-transacted connection throws. When no ambient transaction exists (the normal
        // case: CRM/MasterData callers hold their own DbContext, or ActorContextMiddleware calling
        // through PrincipalResolver), this method owns its own transaction end-to-end.
        if (context.Database.CurrentTransaction is not null)
            return await EvaluateAsync(request, cancellationToken);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(request.Actor.TenantId, cancellationToken);

        var decision = await EvaluateAsync(request, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return decision;
    }

    private async Task<AuthorizationDecision> EvaluateAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        var decisionId = Guid.NewGuid();
        var revision = await GetRevisionAsync(request.Actor.TenantId, cancellationToken);

        if (!await actionCatalog.IsActiveAsync(request.Action, cancellationToken))
            return Deny("action_not_registered", decisionId, revision, AuthorizationDenialStage.Coarse);

        var accountId = await principalResolver.ResolveAccountIdAsync(request.Actor.Principal, cancellationToken);
        if (accountId is null)
            return Deny("principal_not_recognized", decisionId, revision, AuthorizationDenialStage.Coarse);

        var now = DateTimeOffset.UtcNow;
        var items = await context.RoleAssignments
            .Where(a => a.TenantId == request.Actor.TenantId && a.AccountId == accountId
                && a.ValidFrom <= now && (a.ValidTo == null || now < a.ValidTo))
            .Join(context.RolePermissionSets, a => a.RoleId, rps => rps.RoleId, (a, rps) => rps.PermissionSetId)
            .Join(context.PermissionSetItems, psId => psId, i => i.PermissionSetId, (psId, i) => i)
            .Where(i => i.ActionKey == request.Action.Value)
            .ToListAsync(cancellationToken);

        if (items.Any(i => i.Relation is null))
            return Allow("tenant_scope_grant", decisionId, revision);

        if (items.Any(i => i.Relation == PermissionSetItem.OwnerRelation)
            && request.Resource.OwnerPrincipal is { } owner
            && owner == request.Actor.Principal)
            return Allow("owner_relation_grant", decisionId, revision);

        // items.Count == 0: the actor has no grant for this action at all — coarse, no
        // resource-specific fact was ever consulted. items.Count > 0: at least one grant
        // exists (e.g. an owner-relation grant) but none of them covered this resource —
        // a PEP may collapse this into the same external shape as "not found" (tenant
        // non-leak rule), which a Coarse denial must never be.
        var denialStage = items.Count == 0 ? AuthorizationDenialStage.Coarse : AuthorizationDenialStage.Record;
        return Deny("no_matching_grant", decisionId, revision, denialStage);
    }

    private async Task<long> GetRevisionAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        await context.TenantAccessStates
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.Revision)
            .SingleOrDefaultAsync(cancellationToken);

    private static AuthorizationDecision Allow(string reason, Guid decisionId, long revision) =>
        new(AuthorizationEffect.Allow, reason, decisionId, revision);

    private static AuthorizationDecision Deny(string reason, Guid decisionId, long revision, AuthorizationDenialStage denialStage) =>
        new(AuthorizationEffect.Deny, reason, decisionId, revision, denialStage);
}
