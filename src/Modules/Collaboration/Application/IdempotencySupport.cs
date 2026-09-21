using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Collaboration.Domain;
using Collaboration.Idempotency;
using Collaboration.Persistence;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Collaboration.Application;

/// <summary>What the calendar command handlers share around the module-local idempotency table: key validation, the
/// canonical request hash, the lookup, and unique-violation detection for the replay race.</summary>
internal static class IdempotencySupport
{
    public const int MaxKeyLength = 128;
    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    // PostgreSQL error code 23505 = unique_violation (Npgsql exposes SqlState as this raw string).
    private const string UniqueViolationSqlState = "23505";

    public static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength)
            throw new ArgumentException($"Idempotency key must be non-blank and at most {MaxKeyLength} characters.", nameof(key));
    }

    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState };

    /// <summary>Canonical, unambiguous serialization for idempotency keying: JSON of a fixed, explicitly ordered field
    /// set (callers build it), so no field boundary can be forged by field content.</summary>
    public static string Hash(object canonicalFields)
    {
        var json = JsonSerializer.Serialize(canonicalFields);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    public static string? Instant(DateTimeOffset? value) =>
        value is { } instant ? TimestampPrecision.Truncate(instant).ToString("O", CultureInfo.InvariantCulture) : null;

    public static string? Date(DateOnly? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static Task<IdempotencyRecord?> FindAsync(
        CollaborationDbContext context, TenantId tenantId, PrincipalRef principal, string operation, string key,
        CancellationToken cancellationToken) =>
        context.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(
            r => r.TenantId == tenantId
                && r.PrincipalIssuer == principal.Issuer
                && r.PrincipalSubject == principal.Subject
                && r.Operation == operation
                && r.IdempotencyKey == key,
            cancellationToken);
}
