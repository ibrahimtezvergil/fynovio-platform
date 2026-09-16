using Contracts;

namespace MasterData.Application;

public sealed record MergePartyCommand(
    TenantId TenantId,
    long SourcePartyId,
    long TargetPartyId,
    PrincipalRef Principal,
    string IdempotencyKey,
    Guid CorrelationId);
