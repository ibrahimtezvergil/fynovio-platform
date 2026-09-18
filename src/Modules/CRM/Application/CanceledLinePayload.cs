namespace CRM.Application;

internal sealed record CanceledLinePayload(long OpportunityId, long LineId, string CancelReason);
