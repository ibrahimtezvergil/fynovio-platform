namespace CRM.Application;

/// <summary>Mirrors Access.Application.AuthorizationDeniedException's shape but stays
/// inside CRM (AGENTS.md: a module references Contracts only, never another module's
/// exception types either).</summary>
public sealed class OpportunityAuthorizationDeniedException : InvalidOperationException
{
    public OpportunityAuthorizationDeniedException(string actionKey, string reasonCode)
        : base($"Action '{actionKey}' was denied ({reasonCode}).")
    {
    }
}
