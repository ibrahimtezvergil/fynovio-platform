namespace CRM.Application;

public sealed record AddOpportunityLineResult(long OpportunityId, long LineId, bool Replayed);
