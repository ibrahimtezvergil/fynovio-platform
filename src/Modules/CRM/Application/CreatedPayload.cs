namespace CRM.Application;

internal sealed record CreatedPayload(long OpportunityId, long PartyId, string Currency, decimal EstimatedAmount);
