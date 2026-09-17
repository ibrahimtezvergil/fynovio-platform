using Contracts;

namespace Access.Application;

public sealed record RevokeRoleAssignmentCommand(
    TenantId TenantId,
    PrincipalRef RevokedBy,
    long RoleAssignmentId,
    string? Reason,
    Guid CorrelationId,
    string IdempotencyKey);

public sealed record RevokeRoleAssignmentResult(long RoleAssignmentId, bool Replayed);
