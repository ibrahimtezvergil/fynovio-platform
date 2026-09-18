namespace CRM.Application;

internal sealed record LostPayload(long OpportunityId, string LostReason);
