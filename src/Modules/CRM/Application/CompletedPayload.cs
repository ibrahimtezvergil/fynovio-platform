namespace CRM.Application;

internal sealed record CompletedPayload(long OpportunityId, decimal TotalAmount, string Currency);
