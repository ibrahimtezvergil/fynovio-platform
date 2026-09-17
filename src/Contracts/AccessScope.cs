namespace Contracts;

/// <summary>The query-authorization contract's return shape. A domain adapter
/// translates this into a SQL filter — never fetches unauthorized rows and
/// post-filters (round 1 §10.2). An adapter that meets an unrecognized future
/// `ScopeTerm` subtype must treat it as `None` (fail-closed), not skip it.</summary>
public abstract record AccessScope
{
    public sealed record None : AccessScope;
    public sealed record All : AccessScope;
    public sealed record AnyOf(IReadOnlyList<ScopeTerm> Terms) : AccessScope;
}
