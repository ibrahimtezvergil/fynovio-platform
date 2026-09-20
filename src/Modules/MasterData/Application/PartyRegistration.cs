using Contracts;

namespace MasterData.Application;

/// <summary>The Contracts-facing face of <see cref="CreatePartyHandler"/>: maps the module-neutral request to the command.</summary>
public sealed class PartyRegistration(CreatePartyHandler handler) : IPartyRegistration
{
    public async Task<PartyRegistrationResult> RegisterAsync(RegisterPartyRequest request, CancellationToken cancellationToken = default)
    {
        var result = await handler.HandleAsync(
            new CreatePartyCommand(
                request.TenantId, request.PartyType, request.Name, request.Surname, request.Phone, request.Email,
                request.IdempotencyKey, request.CorrelationId),
            cancellationToken);
        return new PartyRegistrationResult(new PartyRef(request.TenantId, result.PartyId), result.Replayed);
    }
}
