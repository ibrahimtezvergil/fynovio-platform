namespace Access.Domain.Authorization;

/// <summary>Access-internal closed set (round 1 decision: "principal_type + principal_id
/// freeze, 1.5'te yalnız user"; gap-closure §8 — deliberately NOT a Contracts type,
/// since the cross-module identity key stays `PrincipalRef`). Adding `Service`/`Agent`/
/// `Group` later is a new enum member + CHECK constraint value, not a redesign.</summary>
public enum PrincipalType
{
    User
}
