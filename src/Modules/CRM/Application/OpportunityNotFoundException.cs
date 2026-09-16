namespace CRM.Application;

/// <summary>No opportunity with this id is visible in the caller's tenant. Under RLS another
/// tenant's row is indistinguishable from a missing one, by design.</summary>
public sealed class OpportunityNotFoundException : InvalidOperationException
{
    public OpportunityNotFoundException(long opportunityId)
        : base($"Opportunity {opportunityId} was not found.")
    {
    }
}
