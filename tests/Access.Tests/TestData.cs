using Contracts;

namespace Access.Tests;

/// <summary>Mirrored from CRM.Tests.TestData: each test runs with its own tenant to prevent
/// data leakage across tests (AGENTS.md: no fixed IDs in tests).</summary>
public static class TestData
{
    private static long _nextTenantId = 1000;

    public static TenantId NextTenant() => new(Interlocked.Increment(ref _nextTenantId));
}
