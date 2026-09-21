using Contracts;

namespace Collaboration.Application;

/// <summary>Authorization denial with full denial context — includes `DenialStage`
/// and optional `EntryId` for internal diagnostics/logging. For List operations,
/// entry-scoped denials are indistinguishable from not-found (tenant non-leak rule).</summary>
public sealed class CalendarEntryAuthorizationDeniedException : Exception
{
    public string ActionKey { get; }
    public string ReasonCode { get; }
    public AuthorizationDenialStage DenialStage { get; }
    public long? EntryId { get; }

    public CalendarEntryAuthorizationDeniedException(string actionKey, string reasonCode, AuthorizationDenialStage denialStage, long? entryId = null)
        : base($"Action '{actionKey}' was denied ({reasonCode}).")
    {
        // A Record-stage denial is only meaningful once an entry was loaded, and it is what makes the response a 404. One
        // without an entry id would fall through to 403 and tell the caller the entry exists.
        if (denialStage == AuthorizationDenialStage.Record && entryId is null)
            throw new System.Diagnostics.UnreachableException("A Record-stage denial must name the entry it was made for.");

        ActionKey = actionKey;
        ReasonCode = reasonCode;
        DenialStage = denialStage;
        EntryId = entryId;
    }
}
