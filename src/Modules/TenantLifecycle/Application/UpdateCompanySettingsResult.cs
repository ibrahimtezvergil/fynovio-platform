namespace TenantLifecycle.Application;

public sealed record UpdateCompanySettingsResult(CompanySettings Settings, bool Replayed);
