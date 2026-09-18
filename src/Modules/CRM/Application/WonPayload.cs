namespace CRM.Application;

internal sealed record WonPayload(long OpportunityId, decimal TotalAmount, string Currency);
