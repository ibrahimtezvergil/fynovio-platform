using Contracts;
using CRM.Application;
using Xunit;

namespace CRM.Tests.Application;

public sealed class CreatePartyReferenceHandlerTests
{
    private static readonly TenantId Tenant = new(7);

    private sealed class FakeRegistration : IPartyRegistration
    {
        public List<RegisterPartyRequest> Requests { get; } = [];

        public Task<PartyRegistrationResult> RegisterAsync(RegisterPartyRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new PartyRegistrationResult(new PartyRef(request.TenantId, 42), Replayed: false));
        }
    }

    private static CreatePartyReferenceCommand Command(
        string name = "Acme", string? surname = null, string? phone = null, string? email = null, PartyType type = PartyType.Organization) =>
        new(Tenant, TestData.Seller, type, name, surname, phone, email, "key-1", Guid.NewGuid());

    [Fact]
    public async Task Is_gated_by_the_party_create_action_with_no_resource()
    {
        var authorizer = new RecordingAuthorizer();

        await new CreatePartyReferenceHandler(authorizer, new FakeRegistration()).HandleAsync(Command());

        Assert.Equal(["crm.reference.party.create"], authorizer.Actions);
    }

    [Fact]
    public async Task Hands_the_request_to_the_registration_and_returns_the_new_id()
    {
        var registration = new FakeRegistration();

        var result = await new CreatePartyReferenceHandler(StubAuthorizer.AlwaysAllow, registration)
            .HandleAsync(Command("Ada", "Lovelace", "+90 555", "ada@analytical.test", PartyType.Person));

        Assert.Equal(new CreatePartyReferenceResult(42, false), result);
        var request = Assert.Single(registration.Requests);
        Assert.Equal((Tenant, PartyType.Person, "Ada", "Lovelace", "+90 555", "ada@analytical.test", "key-1"),
            (request.TenantId, request.PartyType, request.Name, request.Surname, request.Phone, request.Email, request.IdempotencyKey));
    }

    [Fact]
    public async Task A_denied_actor_gets_a_coarse_denial_and_MasterData_is_never_asked_even_for_invalid_input()
    {
        var registration = new FakeRegistration();
        var handler = new CreatePartyReferenceHandler(StubAuthorizer.AlwaysDeny, registration);

        var denied = await Assert.ThrowsAsync<OpportunityAuthorizationDeniedException>(() => handler.HandleAsync(Command(name: " ")));

        Assert.Equal(AuthorizationDenialStage.Coarse, denied.DenialStage);
        Assert.Empty(registration.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_name_is_a_validation_error(string name)
    {
        var registration = new FakeRegistration();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new CreatePartyReferenceHandler(StubAuthorizer.AlwaysAllow, registration).HandleAsync(Command(name)));

        Assert.Empty(registration.Requests);
    }

    [Fact]
    public async Task Over_long_fields_and_an_unknown_party_type_are_validation_errors()
    {
        var registration = new FakeRegistration();
        var handler = new CreatePartyReferenceHandler(StubAuthorizer.AlwaysAllow, registration);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(Command(new string('a', CreatePartyReferenceHandler.MaxNameLength + 1))));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(Command(surname: new string('a', CreatePartyReferenceHandler.MaxNameLength + 1), type: PartyType.Person)));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(Command(phone: new string('1', CreatePartyReferenceHandler.MaxPhoneLength + 1))));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(Command(email: new string('a', CreatePartyReferenceHandler.MaxEmailLength + 1))));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(Command(type: (PartyType)99)));

        Assert.Empty(registration.Requests);
    }
}
