namespace CRM.Application;

public sealed record CompleteOpportunityResult(long OpportunityId, decimal TotalAmount, bool Replayed);
