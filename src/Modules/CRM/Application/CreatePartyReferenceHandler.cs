using Contracts;

namespace CRM.Application;

/// <summary>Lets a CRM user register a new customer when the picker finds none. CRM does not own Parties — MasterData
/// does — so this only authorizes the CRM user (`crm.reference.party.create`, no resource) and hands the request to
/// MasterData through Contracts. Authorization comes first, so a caller without the grant learns nothing about
/// whether the party exists or the key was used. MasterData has no authorization layer of its own yet, so this CRM
/// action is the gate on that write path (Phase 2.6 plan, limitation L-3).</summary>
public sealed class CreatePartyReferenceHandler(IAuthorizer authorizer, IPartyRegistration registration)
{
    private const string ActionKeyValue = CrmActionKeys.PartyReferenceCreate;
    public const int MaxNameLength = 200;
    public const int MaxPhoneLength = 50;
    public const int MaxEmailLength = 320;

    public async Task<CreatePartyReferenceResult> HandleAsync(CreatePartyReferenceCommand command, CancellationToken cancellationToken = default)
    {
        var actor = new ActorContext(command.TenantId, command.Principal, command.CorrelationId);
        var decision = await authorizer.AuthorizeAsync(
            new AuthorizationRequest(actor, new ActionKey(ActionKeyValue), new ResourceDescriptor("PartyReference", null, null)), cancellationToken);
        if (!decision.IsAllowed)
            throw new OpportunityAuthorizationDeniedException(ActionKeyValue, decision.ReasonCode, decision.DenialStage);

        Validate(command);

        var result = await registration.RegisterAsync(
            new RegisterPartyRequest(
                command.TenantId, command.PartyType, command.Name, command.Surname, command.Phone, command.Email,
                command.IdempotencyKey, command.CorrelationId),
            cancellationToken);
        return new CreatePartyReferenceResult(result.PartyRef.PartyId, result.Replayed);
    }

    // Party rows are unbounded text; a reference form has no business storing more than this.
    private static void Validate(CreatePartyReferenceCommand command)
    {
        if (!Enum.IsDefined(command.PartyType))
            throw new ArgumentException("Unknown party type.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.Name) || command.Name.Length > MaxNameLength)
            throw new ArgumentException($"A name of 1–{MaxNameLength} characters is required.", nameof(command));
        if (command.Surname is { } surname && surname.Length > MaxNameLength)
            throw new ArgumentException($"A surname of at most {MaxNameLength} characters is required.", nameof(command));
        if (command.Phone is { } phone && phone.Length > MaxPhoneLength)
            throw new ArgumentException($"A phone number of at most {MaxPhoneLength} characters is required.", nameof(command));
        if (command.Email is { } email && email.Length > MaxEmailLength)
            throw new ArgumentException($"An e-mail address of at most {MaxEmailLength} characters is required.", nameof(command));
    }
}
