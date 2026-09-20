using Contracts;

namespace Access.Application.Authentication;

/// <summary>Refresh an authentication session using the refresh token cookie value.</summary>
public sealed record RefreshSessionCommand(string CookieValue);

public enum RefreshResult
{
    Success,
    SessionInvalid,
    RefreshConflict
}

public sealed record RefreshSessionResult(
    RefreshResult Status,
    long? AccountId = null,
    string? DisplayName = null,
    IReadOnlyList<long>? MembershipTenantIds = null,
    long? SelectedTenantId = null,
    string? AccessToken = null,
    string? RefreshCookie = null,
    Guid? SessionId = null,
    PrincipalRef? Principal = null);
