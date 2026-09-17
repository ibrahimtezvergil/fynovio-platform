namespace Contracts;

/// <summary>What the PEP knows about the resource being authorized — supplied by the
/// owning domain, never read by Access from a private table (round 3 §10 final
/// invariant). `Id` is null for CREATE actions (round 3 §11). `OwnerPrincipal` is the
/// only relationship fact Phase 1.5 evaluates (`relation="owner"`, gap-closure §7);
/// org/territory/attributes are added when those fact providers exist, not reserved
/// empty today (gap-closure §4).</summary>
public readonly record struct ResourceDescriptor
{
    public string ResourceType { get; }
    public long? Id { get; }
    public PrincipalRef? OwnerPrincipal { get; }

    public ResourceDescriptor(string resourceType, long? id, PrincipalRef? ownerPrincipal)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            throw new ArgumentException("Resource type is required.", nameof(resourceType));

        ResourceType = resourceType;
        Id = id;
        OwnerPrincipal = ownerPrincipal;
    }
}
