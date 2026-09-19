using Contracts;

namespace CRM.Application;

/// <summary>Mirrors Access.Application.AuthorizationDeniedException's shape but stays
/// inside CRM (AGENTS.md: a module references Contracts only, never another module's
/// exception types either). Carries the full denial context — including `DenialStage` and
/// `OpportunityId` — for internal diagnostics/logging even though
/// CrmProblemDetailsExceptionHandler externally collapses a `DenialStage.Record` denial
/// into the same 404 shape as OpportunityNotFoundException (tenant non-leak rule; see
/// docs/architecture-analysis/2026-09-19-crm-phase2-authorization-delta-and-entry-stage-resolution.md).
/// `OpportunityId` is null only for CreateOpportunity, where no resource exists yet and
/// `DenialStage` can therefore only ever be `Coarse`.</summary>
public sealed class OpportunityAuthorizationDeniedException : InvalidOperationException
{
    public string ActionKey { get; }
    public string ReasonCode { get; }
    public AuthorizationDenialStage DenialStage { get; }
    public long? OpportunityId { get; }

    public OpportunityAuthorizationDeniedException(string actionKey, string reasonCode, AuthorizationDenialStage denialStage, long? opportunityId = null)
        : base($"Action '{actionKey}' was denied ({reasonCode}).")
    {
        ActionKey = actionKey;
        ReasonCode = reasonCode;
        DenialStage = denialStage;
        OpportunityId = opportunityId;
    }
}
