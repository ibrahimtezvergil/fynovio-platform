namespace CRM.Application;

internal sealed record StageChangedPayload(long OpportunityId, long? FromStageId, long ToStageId);
