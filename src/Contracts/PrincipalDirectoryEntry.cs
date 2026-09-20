namespace Contracts;

/// <summary>Display-oriented snapshot of a tenant member, returned by <see cref="IAuthorizedPrincipalDirectory"/>.
/// Never a tracked entity and never an identity key — `Principal` is; name and e-mail are profile data.</summary>
public sealed record PrincipalDirectoryEntry(PrincipalRef Principal, string DisplayName, string? Email);
