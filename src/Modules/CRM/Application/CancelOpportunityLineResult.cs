namespace CRM.Application;

public sealed record CancelOpportunityLineResult(long OpportunityId, long LineId, bool Replayed);
