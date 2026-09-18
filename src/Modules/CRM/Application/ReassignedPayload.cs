namespace CRM.Application;

internal sealed record ReassignedPayload(long OpportunityId, string PreviousPrincipal, string NewPrincipal);
