using Contracts;

namespace Access.Application.Authentication;

/// <summary>Select an active tenant for the session (via refresh cookie).</summary>
public sealed record SelectTenantCommand(string CookieValue, TenantId TenantId, string? CorrelationId = null, string? IpHash = null);

public enum TenantSelectionStatus
{
    Success,
    SessionInvalid,
    TenantNotPermitted
}

public sealed record SelectTenantResult(
    TenantSelectionStatus Status,
    long? SelectedTenantId = null,
    string? AccessToken = null,
    Guid? SessionId = null,
    PrincipalRef? Principal = null);
