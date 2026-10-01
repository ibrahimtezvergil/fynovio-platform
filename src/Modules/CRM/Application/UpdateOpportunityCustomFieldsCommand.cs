using System.Text.Json;
using Contracts;

namespace CRM.Application;

/// <summary>Full replacement of the active custom field values (adr-tier1-custom-fields.md decision 8).
/// Deprecated values already stored are carried forward by the server.</summary>
public sealed record UpdateOpportunityCustomFieldsCommand(
    TenantId TenantId,
    long OpportunityId,
    PrincipalRef Principal,
    long ExpectedVersion,
    JsonElement? CustomFields,
    string IdempotencyKey,
    Guid CorrelationId);

public sealed record UpdateOpportunityCustomFieldsResult(long OpportunityId, bool Replayed);

internal sealed record CustomFieldsChangedPayload(long OpportunityId, IReadOnlyList<string> ChangedKeys);
