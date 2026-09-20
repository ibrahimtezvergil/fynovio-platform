using System.Text.Json;
using System.Text.Json.Serialization;
using Access.Domain.Authentication;
using Access.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Access.Application.Authentication;

/// <summary>Append-only writer for authentication events. Sanitizes detail payloads to an
/// allow-list to prevent accidental logging of secrets (passwords, tokens, hashes).</summary>
public sealed class AuthEventWriter
{
    private readonly AccessDbContext _context;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Allow-listed keys for event detail payloads
    private static readonly HashSet<string> AllowedDetailKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "outcome",
        "accountId",
        "tenantId",
        "sessionId",
        "reason",
        "attemptCount",
        "lockoutUntil"
    };

    public AuthEventWriter(AccessDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Write an authentication event to the append-only log.</summary>
    public async Task WriteAsync(
        string eventType,
        string outcome,
        DateTimeOffset occurredAt,
        long? accountId = null,
        long? tenantId = null,
        Guid? sessionId = null,
        string? correlationId = null,
        string? ipHash = null,
        Dictionary<string, object?>? detail = null,
        CancellationToken cancellationToken = default)
    {
        // Sanitize detail to allow-list
        string? sanitizedDetail = null;
        if (detail is not null && detail.Count > 0)
        {
            var allowed = new Dictionary<string, object?>();
            foreach (var (key, value) in detail)
            {
                if (AllowedDetailKeys.Contains(key))
                    allowed[key] = value;
            }

            if (allowed.Count > 0)
                sanitizedDetail = JsonSerializer.Serialize(allowed, JsonOptions);
        }

        var evt = AuthEvent.Create(
            eventType,
            outcome,
            occurredAt,
            accountId,
            tenantId,
            sessionId,
            correlationId,
            ipHash,
            sanitizedDetail);

        _context.AuthEvents.Add(evt);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
