using Contracts;
using MasterData.Application;

namespace Host.Authentication;

/// <summary>Development-only sample Parties so the opportunity form's customer picker has something to search. There is
/// no HTTP or production path that creates Parties yet (Phase 2.6 plan, P2); the seed uses the real
/// <see cref="CreatePartyHandler"/> with fixed idempotency keys, so re-running it never duplicates a party.</summary>
public static class PartyDevSeed
{
    private sealed record Sample(PartyType Type, string Name, string? Surname, string? Email);

    private static readonly Dictionary<long, Sample[]> ByTenant = new()
    {
        [1] =
        [
            new(PartyType.Organization, "Acme Corporation", null, "contact@acme.test"),
            new(PartyType.Organization, "Globex Ltd", null, "hello@globex.test"),
            new(PartyType.Organization, "Initech", null, null),
            new(PartyType.Person, "Ada", "Lovelace", "ada@analytical.test"),
            new(PartyType.Person, "Grace", "Hopper", "grace@navy.test"),
            new(PartyType.Person, "Alan", "Turing", null)
        ],
        [2] =
        [
            new(PartyType.Organization, "Umbrella Holdings", null, "office@umbrella.test"),
            new(PartyType.Organization, "Wayne Enterprises", null, null),
            new(PartyType.Person, "Diana", "Prince", "diana@themyscira.test")
        ]
    };

    public static async Task EnsurePartiesAsync(CreatePartyHandler handler, TenantId tenantId, CancellationToken cancellationToken)
    {
        if (!ByTenant.TryGetValue(tenantId.Value, out var samples))
            return;

        for (var i = 0; i < samples.Length; i++)
        {
            var sample = samples[i];
            await handler.HandleAsync(
                new CreatePartyCommand(tenantId, sample.Type, sample.Name, sample.Surname, null, sample.Email, $"dev-seed-party-{i + 1}", Guid.NewGuid()),
                cancellationToken);
        }
    }
}
