namespace TenantLifecycle.Application;

public sealed class CompanySettingsNotFoundException : Exception
{
    public CompanySettingsNotFoundException() : base("Company settings have not been provisioned for this tenant.") { }
}
