namespace Contracts;

/// <summary>Creates a Party on behalf of a consumer that lets a user register a new customer (CRM's "customer not found,
/// add one"). MasterData owns Parties and this is its only cross-module write surface; the consumer authorizes its
/// own user before calling. Idempotent per (tenant, key): a retry with the same key and request returns the original
/// party with `Replayed = true`, and the same key with a different request is refused.</summary>
public interface IPartyRegistration
{
    Task<PartyRegistrationResult> RegisterAsync(RegisterPartyRequest request, CancellationToken cancellationToken = default);
}

public sealed record RegisterPartyRequest(
    TenantId TenantId,
    PartyType PartyType,
    string Name,
    string? Surname,
    string? Phone,
    string? Email,
    string IdempotencyKey,
    Guid CorrelationId);

public sealed record PartyRegistrationResult(PartyRef PartyRef, bool Replayed);
