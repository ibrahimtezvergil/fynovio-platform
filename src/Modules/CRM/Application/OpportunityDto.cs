using System.Text.Json;
using CRM.Customization;
using CRM.Domain;

namespace CRM.Application;

public sealed record OpportunityLineDto(long Id, int Quantity, decimal UnitPrice, decimal? LineTotal, bool IsOptional, bool IsCanceled);

public sealed record OpportunityDto(
    long Id,
    OpportunityStatus Status,
    long PartyId,
    long? OpportunityTypeId,
    string AssignedPrincipalIssuer,
    string AssignedPrincipalSubject,
    string? AssignedPrincipalDisplayName,
    string Currency,
    decimal EstimatedAmount,
    decimal? TotalAmount,
    long? PipelineDefinitionVersionId,
    long? PipelineStageId,
    string? LostReason,
    DateTimeOffset? ExpiryDate,
    DateTimeOffset? OpenedDate,
    DateTimeOffset? WonDate,
    DateTimeOffset? LostDate,
    long RowVersion,
    bool IsArchived,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<OpportunityLineDto> Lines)
{
    /// <summary>Stored custom field object, including values of deprecated fields (read-only in the UI).</summary>
    public JsonElement? CustomFields { get; init; }

    public static OpportunityDto From(Opportunity opportunity) => new(
        opportunity.Id,
        opportunity.Status,
        opportunity.PartyRefPartyId,
        opportunity.OpportunityTypeId,
        opportunity.AssignedPrincipalIssuer,
        opportunity.AssignedPrincipalSubject,
        null,
        opportunity.Currency,
        opportunity.EstimatedAmount,
        opportunity.TotalAmount,
        opportunity.PipelineDefinitionVersionId,
        opportunity.PipelineStageId,
        opportunity.LostReason,
        opportunity.ExpiryDate,
        opportunity.OpenedDate,
        opportunity.WonDate,
        opportunity.LostDate,
        opportunity.RowVersion,
        opportunity.IsArchived,
        opportunity.ArchivedAt,
        opportunity.Lines.Select(l => new OpportunityLineDto(l.Id, l.Quantity, l.UnitPrice, l.LineTotal, l.IsOptional, l.IsCanceled)).ToList())
    {
        CustomFields = CustomFieldValues.ToElement(opportunity.CustomFields)
    };
}
