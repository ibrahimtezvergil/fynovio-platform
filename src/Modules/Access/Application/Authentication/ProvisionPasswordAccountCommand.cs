using Contracts;

namespace Access.Application.Authentication;

/// <summary>Provision a new password-based account. Idempotent by normalized login email.</summary>
public sealed record ProvisionPasswordAccountCommand(
    string Email,
    string DisplayName,
    string Password,
    string PlatformIssuer,
    TenantId? InitialTenantId = null,
    string? Locale = null);

public sealed record ProvisionPasswordAccountResult(
    long AccountId,
    PrincipalRef Principal);
