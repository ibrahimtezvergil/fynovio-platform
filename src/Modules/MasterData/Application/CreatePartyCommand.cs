using Contracts;

namespace MasterData.Application;

public sealed record CreatePartyCommand(
    TenantId TenantId,
    PartyType PartyType,
    string Name,
    string? Surname,
    string? Phone,
    string? Email,
    string IdempotencyKey,
    Guid CorrelationId);
