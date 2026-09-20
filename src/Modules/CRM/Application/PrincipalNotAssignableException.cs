using Contracts;

namespace CRM.Application;

/// <summary>The chosen assignee is not an active member permitted to hold this kind of opportunity (or is not a
/// member at all). Deliberately says nothing about which of those it was — no membership oracle.</summary>
public sealed class PrincipalNotAssignableException(PrincipalRef principal)
    : InvalidOperationException($"'{principal}' cannot be assigned opportunities in this tenant.");
