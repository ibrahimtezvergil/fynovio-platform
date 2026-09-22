using Access.Domain.Identity;
using Access.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Access.Application;

/// <summary>Tenant-scoped, administrative read model for the workspace access console.
/// It deliberately exposes only active role assignments; historical grants stay in evidence,
/// not in the day-to-day member list.</summary>
public sealed record GetTenantAccessOverviewQuery(TenantId TenantId, PrincipalRef RequestedBy, Guid CorrelationId);

public sealed record TenantAccessOverview(IReadOnlyList<TenantMemberSummary> Members, IReadOnlyList<TenantRoleSummary> Roles);

public sealed record TenantMemberSummary(
    string PrincipalIssuer,
    string PrincipalSubject,
    string DisplayName,
    string Email,
    string Status,
    IReadOnlyList<TenantRoleAssignmentSummary> Assignments);

public sealed record TenantRoleAssignmentSummary(long AssignmentId, string RoleKey, string RoleName);

public sealed record TenantRoleSummary(string Key, string Name, string Origin, IReadOnlyList<string> ActionKeys);

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
            select new { assignment.AccountId, assignment.Id, role.Key, role.Name })
            .ToListAsync(cancellationToken);

        var roles = await context.Roles.AsNoTracking()
            .Where(role => role.TenantId == query.TenantId)
            .OrderBy(role => role.Name)
            .Select(role => new { role.Id, role.Key, role.Name, role.Origin })
            .ToListAsync(cancellationToken);

        var roleActionKeys = await (
            from rolePermissionSet in context.RolePermissionSets.AsNoTracking()
            join permissionSetItem in context.PermissionSetItems.AsNoTracking() on rolePermissionSet.PermissionSetId equals permissionSetItem.PermissionSetId
            where rolePermissionSet.TenantId == query.TenantId && permissionSetItem.TenantId == query.TenantId
            select new { rolePermissionSet.RoleId, permissionSetItem.ActionKey })
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new TenantAccessOverview(
            members.Select(member => new TenantMemberSummary(
                member.Issuer, member.Subject, member.DisplayName, member.Email, ToWireStatus(member.Status),
                assignments.Where(assignment => assignment.AccountId == member.AccountId)
                    .Select(assignment => new TenantRoleAssignmentSummary(assignment.Id, assignment.Key, assignment.Name))
                    .OrderBy(assignment => assignment.RoleName)
                    .ToArray())).ToArray(),
            roles.Select(role => new TenantRoleSummary(
                role.Key, role.Name, role.Origin,
                roleActionKeys.Where(item => item.RoleId == role.Id).Select(item => item.ActionKey).Order().ToArray())).ToArray());
    }

    private static string ToWireStatus(MembershipStatus status) => status switch
    {
        MembershipStatus.Invited => "invited",
        MembershipStatus.Active => "active",
        MembershipStatus.Disabled => "disabled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
