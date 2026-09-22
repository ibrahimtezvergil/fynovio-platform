namespace TenantLifecycle.Domain;

public sealed record TenantProfileDetails(
    string DisplayName,
    string? LegalName,
    string? TaxNumber,
    string? TaxOffice,
    string? Email,
    string? Phone,
    string? Address,
    string Timezone,
    string CurrencyCode);
