namespace TenantLifecycle.Application;

public sealed class CompanySettingsAuthorizationDeniedException(string actionKey, string reasonCode) : Exception($"Actor is not allowed to perform '{actionKey}' ({reasonCode}).")
{
    public string ActionKey { get; } = actionKey;
    public string ReasonCode { get; } = reasonCode;
}
