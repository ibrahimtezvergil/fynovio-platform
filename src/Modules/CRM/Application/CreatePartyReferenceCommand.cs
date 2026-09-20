using Contracts;

namespace CRM.Application;

public sealed record CreatePartyReferenceCommand(
    TenantId TenantId,
    PrincipalRef Principal,
    PartyType PartyType,
    string Name,
    string? Surname,
    string? Phone,
    string? Email,
    string IdempotencyKey,
    Guid CorrelationId);

public sealed record CreatePartyReferenceResult(long Id, bool Replayed);
