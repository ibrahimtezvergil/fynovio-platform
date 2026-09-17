using Contracts;

namespace Access.Application;

public sealed record GrantRoleAssignmentCommand(
    TenantId TenantId,
    PrincipalRef GrantedBy,
    PrincipalRef Grantee,
    string RoleKey,
    string? Reason,
    Guid CorrelationId,
    string IdempotencyKey);

public sealed record GrantRoleAssignmentResult(long RoleAssignmentId, bool Replayed);
