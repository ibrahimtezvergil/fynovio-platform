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
        ActionKey = actionKey;
        ReasonCode = reasonCode;
        DenialStage = denialStage;
        EntryId = entryId;
    }
}
