using System.Text.Json;
using TenantLifecycle.Domain;

namespace TenantLifecycle.Application;

internal static class TenantProfileOutbox
{
    public const string EventSource = "/enterprise/tenant-lifecycle";
    public const string ProvisionedEventType = "enterprise.tenant-lifecycle.tenant-profile.provisioned.v1";
    public const string UpdatedEventType = "enterprise.tenant-lifecycle.tenant-profile.updated.v1";

    public static string Subject(long tenantId) => $"tenant-profiles/{tenantId}";

    public static string Payload(TenantProfile profile) => JsonSerializer.Serialize(
        new TenantProfileFact(profile.TenantId.Value, profile.RowVersion, profile.UpdatedAt));

    private sealed record TenantProfileFact(long TenantId, long RowVersion, DateTimeOffset UpdatedAt);
}
