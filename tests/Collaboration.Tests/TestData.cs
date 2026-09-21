using Contracts;

namespace Collaboration.Tests;

/// <summary>Test data helpers. Each test gets its own tenant ID from an incrementing counter
/// to avoid data leakage between tests (AGENTS.md: no magic constants in tests).</summary>
public static class TestData
{
    private static long _nextTenantId = 10000;

    public static TenantId NextTenant() => new(Interlocked.Increment(ref _nextTenantId));
}
