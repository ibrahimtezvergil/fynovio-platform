namespace Contracts;

/// <summary>The trusted, per-request actor — built once from the authenticated
/// session, never from a caller-supplied command body (round 3 §4 final pipeline,
/// step 2-3). Deliberately minimal: no `ActingFor`/`ImpersonatedBy` yet — those are
/// DESIGN/FREEZE (gap-closure §4) until a real on-behalf-of scenario exists.</summary>
public readonly record struct ActorContext
{
    public TenantId TenantId { get; }
    public PrincipalRef Principal { get; }
    public Guid CorrelationId { get; }

    public ActorContext(TenantId tenantId, PrincipalRef principal, Guid correlationId)
    {
        TenantId = tenantId;
        Principal = principal;
        CorrelationId = correlationId;
    }
}
