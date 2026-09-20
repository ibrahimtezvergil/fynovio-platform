using Contracts;

namespace CRM.Application;

/// <summary>What CRM requires of anyone an opportunity is assigned to. The requirement is CRM's own business rule
/// (declared here, once); WHO satisfies it is Access's answer, evaluated by the PDP's grants — neither the
/// frontend nor this module filters candidates. An assignee must at least see the opportunity and be able to move
/// it through the pipeline: a read-only member is not a sensible owner.</summary>
public static class CrmAssignmentPolicy
{
    public static readonly IReadOnlyList<ActionKey> RequiredAssigneeActions =
    [
        new(CrmActionKeys.OpportunityRead),
        new(CrmActionKeys.OpportunityChangeStage)
    ];
}
