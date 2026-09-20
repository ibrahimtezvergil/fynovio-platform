using Contracts;

namespace Access.Application.Authentication;

/// <summary>Authenticate a user with email and password. Creates a session and refresh token.</summary>
public sealed record AuthenticateCommand(string Email, string Password, string? CorrelationId = null);

public enum AuthenticationStatus
{
    Authenticated,
    TenantSelectionRequired,
    NoMembership,
    InvalidCredentials
}

public sealed record AuthenticateResult(
    AuthenticationStatus Status,
    long? AccountId = null,
    string? DisplayName = null,
    IReadOnlyList<long>? MembershipTenantIds = null,
    long? SelectedTenantId = null,
    string? AccessToken = null,
    string? RefreshCookie = null,
    Guid? SessionId = null,
    PrincipalRef? Principal = null);
