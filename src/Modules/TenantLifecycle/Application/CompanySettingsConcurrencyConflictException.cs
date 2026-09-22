namespace TenantLifecycle.Application;

public sealed class CompanySettingsConcurrencyConflictException(long expectedVersion, long? actualVersion = null) : Exception("Company settings were changed by another request.")
{
    public long ExpectedVersion { get; } = expectedVersion;
    public long? ActualVersion { get; } = actualVersion;
}
