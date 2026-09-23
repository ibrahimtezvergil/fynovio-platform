using Access.Domain.Identity;
using Access.Domain.Authentication;
using Access.Domain.Authorization;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

/// <summary>Tenant-scoped, administrative read model for the workspace access console.
/// It deliberately exposes only active role assignments; historical grants stay in evidence,
/// not in the day-to-day member list.</summary>
public sealed record GetTenantAccessOverviewQuery(TenantId TenantId, PrincipalRef RequestedBy, Guid CorrelationId);

public sealed record TenantAccessOverview(IReadOnlyList<TenantMemberSummary> Members, IReadOnlyList<TenantRoleSummary> Roles,
    IReadOnlyList<TenantAccessActionSummary> AvailableActions, IReadOnlyList<PendingInvitationSummary> PendingInvitations,
    long Revision, bool CanInvite, bool CanGrant, bool CanRevoke, bool CanManageRoles);

public sealed record PendingInvitationSummary(Guid InvitationId, string Email, string? DisplayName, string? RoleKey,
    DateTimeOffset ExpiresAt);

public sealed record TenantMemberSummary(
    string PrincipalIssuer,
    string PrincipalSubject,
    string DisplayName,
    string Email,
    string Status,
    IReadOnlyList<TenantRoleAssignmentSummary> Assignments);

public sealed record TenantRoleAssignmentSummary(long AssignmentId, string RoleKey, string RoleName, bool CanRevoke);

public sealed record TenantRolePermissionSummary(string ActionKey, string? Relation);

public sealed record TenantRoleSummary(string Key, string Name, string Origin, bool CanEdit, IReadOnlyList<TenantRolePermissionSummary> Permissions);

public sealed record TenantAccessActionSummary(string Key, string OwnerModule, string ResourceType);

public sealed class GetTenantAccessOverviewHandler(AccessDbContext context, IAuthorizer authorizer)
{
    public async Task<TenantAccessOverview> HandleAsync(GetTenantAccessOverviewQuery query, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(query.TenantId, cancellationToken);

        var actor = new ActorContext(query.TenantId, query.RequestedBy, query.CorrelationId);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey("access.role.manage"), new ResourceDescriptor("Access.Role", null, null)),
            cancellationToken);
        if (!decision.IsAllowed)
            throw new AuthorizationDeniedException("access.role.manage", decision.ReasonCode);

        var canInvite = (await authorizer.AuthorizeAsync(new AuthorizationRequest(actor,
            new ActionKey("identity.membership.invite"), new ResourceDescriptor("Identity.TenantMembership", null, null)),
            cancellationToken)).IsAllowed;
        var canGrant = (await authorizer.AuthorizeAsync(new AuthorizationRequest(actor,
            new ActionKey("access.role_assignment.grant"), new ResourceDescriptor("Access.RoleAssignment", null, null)),
            cancellationToken)).IsAllowed;
        var canRevoke = (await authorizer.AuthorizeAsync(new AuthorizationRequest(actor,
            new ActionKey("access.role_assignment.revoke"), new ResourceDescriptor("Access.RoleAssignment", null, null)),
            cancellationToken)).IsAllowed;
        var canManagePermissionSets = (await authorizer.AuthorizeAsync(new AuthorizationRequest(actor,
            new ActionKey("access.permission_set.manage"), new ResourceDescriptor("Access.PermissionSet", null, null)),
            cancellationToken)).IsAllowed;

        var members = await (
            from membership in context.TenantMemberships.AsNoTracking()
            join account in context.Accounts.AsNoTracking() on membership.AccountId equals account.Id
            join identity in context.ExternalIdentities.AsNoTracking() on account.Id equals identity.AccountId
            where membership.TenantId == query.TenantId
            orderby account.DisplayName, account.Email
            select new { identity.Issuer, identity.Subject, account.DisplayName, account.Email, membership.Status, membership.AccountId })
            .ToListAsync(cancellationToken);

        var assignments = await (
            from assignment in context.RoleAssignments.AsNoTracking()
            join role in context.Roles.AsNoTracking() on assignment.RoleId equals role.Id
            where assignment.TenantId == query.TenantId && assignment.ValidTo == null
                && (role.Key == BootstrapTenantAccessHandler.TenantAdministratorRoleKey || role.Origin == Role.OriginTenant)
            select new { assignment.AccountId, assignment.Id, role.Key, role.Name })
            .ToListAsync(cancellationToken);

        var roles = await context.Roles.AsNoTracking()
            // Module templates remain internal capability grants. The workspace console starts
            // with its one administrator role and subsequently shows tenant-created roles.
            .Where(role => role.TenantId == query.TenantId
                && (role.Key == BootstrapTenantAccessHandler.TenantAdministratorRoleKey || role.Origin == Role.OriginTenant))
            .OrderBy(role => role.Name)
            .Select(role => new { role.Id, role.Key, role.Name, role.Origin })
            .ToListAsync(cancellationToken);

        var availableActions = await context.Actions.AsNoTracking()
            .Where(action => !action.IsDeprecated)
            .OrderBy(action => action.OwnerModule).ThenBy(action => action.ActionKey)
            .Select(action => new TenantAccessActionSummary(action.ActionKey, action.OwnerModule, action.ResourceType))
            .ToListAsync(cancellationToken);
        var revision = await context.TenantAccessStates.AsNoTracking()
            .Where(state => state.TenantId == query.TenantId).Select(state => state.Revision).SingleAsync(cancellationToken);

        var roleActionKeys = await (
            from rolePermissionSet in context.RolePermissionSets.AsNoTracking()
            join permissionSetItem in context.PermissionSetItems.AsNoTracking() on rolePermissionSet.PermissionSetId equals permissionSetItem.PermissionSetId
            where rolePermissionSet.TenantId == query.TenantId && permissionSetItem.TenantId == query.TenantId
            select new { rolePermissionSet.RoleId, permissionSetItem.ActionKey, permissionSetItem.Relation })
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var pendingInvitations = await context.AccountTokens.AsNoTracking()
            .Where(token => token.Purpose == AccountTokenPurpose.Invite && token.TenantId == query.TenantId.Value
                && token.ConsumedAt == null && token.RevokedAt == null && token.ExpiresAt > now)
            .OrderByDescending(token => token.CreatedAt)
            .Select(token => new PendingInvitationSummary(token.Id, token.EmailNormalized!, token.DisplayName,
                token.InvitedRoleKey, token.ExpiresAt))
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var assignmentsByAccount = assignments.ToLookup(assignment => assignment.AccountId);
        var actionKeysByRole = roleActionKeys.ToLookup(item => item.RoleId);
        var activeAdministrators = assignments
            .Where(assignment => assignment.Key == BootstrapTenantAccessHandler.TenantAdministratorRoleKey
                && members.Any(member => member.AccountId == assignment.AccountId && member.Status == MembershipStatus.Active))
            .Select(assignment => assignment.AccountId).Distinct().Count();

        return new TenantAccessOverview(
            members.Select(member => new TenantMemberSummary(
                member.Issuer, member.Subject, member.DisplayName, member.Email, ToWireStatus(member.Status),
                assignmentsByAccount[member.AccountId]
                    .Select(assignment => new TenantRoleAssignmentSummary(assignment.Id, assignment.Key, assignment.Name,
                        assignment.Key != BootstrapTenantAccessHandler.TenantAdministratorRoleKey || activeAdministrators > 1))
                    .OrderBy(assignment => assignment.RoleName)
                    .ToArray())).ToArray(),
            roles.Select(role => new TenantRoleSummary(
                role.Key, role.Name, role.Origin, canManagePermissionSets && role.Origin == Role.OriginTenant,
                actionKeysByRole[role.Id]
                    .Select(item => new TenantRolePermissionSummary(item.ActionKey, item.Relation))
                    .Distinct().OrderBy(item => item.ActionKey).ThenBy(item => item.Relation).ToArray())).ToArray(),
            availableActions, pendingInvitations, revision, canInvite, canGrant, canRevoke, canManagePermissionSets);
    }

    private static string ToWireStatus(MembershipStatus status) => status switch
    {
        MembershipStatus.Invited => "invited",
        MembershipStatus.Active => "active",
        MembershipStatus.Disabled => "disabled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
