namespace Contracts;

/// <summary>`Revision` is the `TenantAccessRevision` the decision was computed against
/// (round 3 §7/§8 freeze #17) — never a second, independently-invented counter.
/// `MatchedGrants`/`Obligations` are not here yet: restriction/field-security/step-up
/// don't exist in Phase 1.5 (gap-closure §4).</summary>
public readonly record struct AuthorizationDecision
{
    public AuthorizationEffect Effect { get; }
    public string ReasonCode { get; }
    public Guid DecisionId { get; }
    public long Revision { get; }

    public AuthorizationDecision(AuthorizationEffect effect, string reasonCode, Guid decisionId, long revision)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
            throw new ArgumentException("Reason code is required.", nameof(reasonCode));

        Effect = effect;
        ReasonCode = reasonCode;
        DecisionId = decisionId;
        Revision = revision;
    }

    public bool IsAllowed => Effect == AuthorizationEffect.Allow;
}
