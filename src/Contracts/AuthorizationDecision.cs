namespace Contracts;

/// <summary>`Revision` is the `TenantAccessRevision` the decision was computed against
/// (round 3 §7/§8 freeze #17) — never a second, independently-invented counter.
/// `MatchedGrants`/`Obligations` are not here yet: restriction/field-security/step-up
/// don't exist in Phase 1.5 (gap-closure §4). `DenialStage` is an additive exception to
/// that: it doesn't add a new policy outcome (`Effect` stays exactly `Allow`/`Deny`), it
/// only surfaces a distinction the PDP already computes internally when denying — see
/// `AuthorizationDenialStage` and the 2026-09-19 authorization-delta doc.</summary>
public readonly record struct AuthorizationDecision
{
    public AuthorizationEffect Effect { get; }
    public string ReasonCode { get; }
    public Guid DecisionId { get; }
    public long Revision { get; }
    public AuthorizationDenialStage DenialStage { get; }

    public AuthorizationDecision(AuthorizationEffect effect, string reasonCode, Guid decisionId, long revision, AuthorizationDenialStage denialStage = AuthorizationDenialStage.None)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
            throw new ArgumentException("Reason code is required.", nameof(reasonCode));

        Effect = effect;
        ReasonCode = reasonCode;
        DecisionId = decisionId;
        Revision = revision;
        DenialStage = denialStage;
    }

    public bool IsAllowed => Effect == AuthorizationEffect.Allow;
}
