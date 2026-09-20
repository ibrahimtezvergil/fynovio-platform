using Contracts;

namespace CRM.Application;

/// <summary>The customer chosen for an opportunity does not exist in this tenant. A party of another tenant is
/// indistinguishable from a missing one (the resolver is tenant-scoped), so this is no existence oracle.</summary>
public sealed class PartyNotFoundException(PartyRef party)
    : InvalidOperationException($"Party {party.PartyId} was not found in this tenant.");
