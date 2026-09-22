using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Contracts;
using Microsoft.EntityFrameworkCore;
using TenantLifecycle.Idempotency;
using TenantLifecycle.Persistence;

namespace TenantLifecycle.Application;

internal static class IdempotencySupport
{
    public const int MaxKeyLength = 128;
    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    public static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength)
            throw new ArgumentException("Idempotency key must be non-blank and at most 128 characters.", nameof(key));
    }

    public static string Hash(object canonicalFields) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonicalFields))));

    public static Task<IdempotencyRecord?> FindAsync(
        TenantLifecycleDbContext context,
        TenantId tenantId,
        PrincipalRef principal,
        string operation,
        string key,
        CancellationToken cancellationToken) =>
        context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
            record => record.TenantId == tenantId
                && record.PrincipalIssuer == principal.Issuer
                && record.PrincipalSubject == principal.Subject
                && record.Operation == operation
                && record.IdempotencyKey == key,
            cancellationToken);
}
