using Contracts;

namespace MasterData.Application;

public sealed record ResolveOrCreatePartyCommand(
    TenantId TenantId,
    string Provider,
    string SourceInstanceRef,
    string? ExternalType,
    string ExternalId,
    PartyType PartyType,
    string Name,
    Guid CorrelationId);
