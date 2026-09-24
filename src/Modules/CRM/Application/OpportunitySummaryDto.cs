using CRM.Domain;

namespace CRM.Application;

public sealed record OpportunitySummaryDto(
    long Id,
    OpportunityStatus Status,
    decimal EstimatedAmount,
    string Currency,
    string AssignedPrincipalIssuer,
    string AssignedPrincipalSubject,
    long? PipelineStageId,
    long PartyId,
    long? PipelineDefinitionVersionId,
    DateTimeOffset? ExpiryDate,
    bool IsArchived,
    DateTimeOffset? ArchivedAt,
    long RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    decimal? TotalAmount,
    IReadOnlyList<string> Needs,
    string? AssignedPrincipalDisplayName);
