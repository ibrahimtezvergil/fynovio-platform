using Contracts;
using System.Text.Json;

namespace CRM.Domain;

/// <summary>Typed, tenant-owned CRM defaults. Platform capabilities such as workflow,
/// custom fields and authorization deliberately do not live here.</summary>
public sealed class CrmSettings
{
    public TenantId TenantId { get; private set; }
    public long? DefaultPipelineDefinitionId { get; private set; }
    public OpportunityCreationMode OpportunityCreationMode { get; private set; }
    public string OpportunityCreationStepsJson { get; private set; } = "[\"Customer\",\"Needs\",\"Products\"]";
    public IReadOnlyList<OpportunityCreationStep> OpportunityCreationSteps => (JsonSerializer.Deserialize<string[]>(OpportunityCreationStepsJson) ?? [])
        .Select(value => Enum.Parse<OpportunityCreationStep>(value, ignoreCase: false)).ToArray();
    public long? DefaultOpportunityTypeId { get; private set; }
    public bool RequireLostReason { get; private set; }
    public bool RequireWonLine { get; private set; } = true;
    public AssignmentMode DefaultAssignmentMode { get; private set; }
    public AssignmentPolicy AssignmentPolicy { get; private set; }
    public string? DefaultPrincipalIssuer { get; private set; }
    public string? DefaultPrincipalSubject { get; private set; }
    public long? DefaultTeamId { get; private set; }
    public long? DefaultTerritoryId { get; private set; }
    public long RowVersion { get; private set; } = 1;
    public DateTimeOffset UpdatedAt { get; private set; }

    private CrmSettings() { }

    public static CrmSettings Create(TenantId tenantId) => new() { TenantId = tenantId, UpdatedAt = DateTimeOffset.UtcNow };

    public void Replace(long? defaultPipelineDefinitionId, OpportunityCreationMode creationMode, long? defaultOpportunityTypeId,
        bool requireLostReason, bool requireWonLine, AssignmentMode assignmentMode, AssignmentPolicy assignmentPolicy,
        PrincipalRef? defaultPrincipal, long? defaultTeamId, long? defaultTerritoryId,
        IReadOnlyList<OpportunityCreationStep>? opportunityCreationSteps = null)
    {
        if (assignmentMode == AssignmentMode.DefaultPrincipal && defaultPrincipal is null)
            throw new ArgumentException("A default principal is required for DefaultPrincipal assignment mode.", nameof(defaultPrincipal));
        if (assignmentMode == AssignmentMode.Team && defaultTeamId is null)
            throw new ArgumentException("A default team is required for Team assignment mode.", nameof(defaultTeamId));
        if (assignmentMode == AssignmentMode.Territory && defaultTerritoryId is null)
            throw new ArgumentException("A default territory is required for Territory assignment mode.", nameof(defaultTerritoryId));
        if (assignmentPolicy == AssignmentPolicy.ManualOnly && assignmentMode != AssignmentMode.Manual)
            throw new ArgumentException("ManualOnly policy requires manual default assignment mode.", nameof(assignmentMode));
        var steps = opportunityCreationSteps ?? [OpportunityCreationStep.Customer, OpportunityCreationStep.Needs, OpportunityCreationStep.Products];
        if (steps.Count is < 1 or > 3 || steps.Distinct().Count() != steps.Count || !steps.Contains(OpportunityCreationStep.Customer))
            throw new ArgumentException("Creation steps must be unique and include customer information.", nameof(opportunityCreationSteps));

        DefaultPipelineDefinitionId = defaultPipelineDefinitionId;
        OpportunityCreationMode = creationMode;
        OpportunityCreationStepsJson = JsonSerializer.Serialize(steps.Select(step => step.ToString()).ToArray());
        DefaultOpportunityTypeId = defaultOpportunityTypeId;
        RequireLostReason = requireLostReason;
        RequireWonLine = requireWonLine;
        DefaultAssignmentMode = assignmentMode;
        AssignmentPolicy = assignmentPolicy;
        DefaultPrincipalIssuer = defaultPrincipal?.Issuer;
        DefaultPrincipalSubject = defaultPrincipal?.Subject;
        DefaultTeamId = defaultTeamId;
        DefaultTerritoryId = defaultTerritoryId;
        RowVersion++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
