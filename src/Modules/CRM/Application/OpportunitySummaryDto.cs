using CRM.Domain;

namespace CRM.Application;

public sealed record OpportunitySummaryDto(
    long Id,
    OpportunityStatus Status,
    decimal EstimatedAmount,
    string Currency,
    string AssignedPrincipalIssuer,
    string AssignedPrincipalSubject,
    long? PipelineStageId);
