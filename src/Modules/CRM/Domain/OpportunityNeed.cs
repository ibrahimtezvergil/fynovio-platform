using Contracts;

namespace CRM.Domain;

/// <summary>Join table restored in schema revision 2 — legacy's sales_opportunity_needs,
/// used to compute estimated_amount from the sum of selected needs' average_price.</summary>
public sealed class OpportunityNeed
{
    public TenantId TenantId { get; private set; }
    public long OpportunityId { get; private set; }
    public long CustomerNeedId { get; private set; }

    private OpportunityNeed() { }

    public static OpportunityNeed Create(TenantId tenantId, long opportunityId, long customerNeedId)
        => new()
        {
            TenantId = tenantId,
            OpportunityId = opportunityId,
            CustomerNeedId = customerNeedId
        };
}
