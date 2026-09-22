namespace TenantLifecycle.Application;

public sealed record CompanySettings(
    string DisplayName,
    string? LegalName,
    string? TaxNumber,
    string? TaxOffice,
    string? Email,
    string? Phone,
    string? Address,
    string Timezone,
    string CurrencyCode,
    long RowVersion);
