using Contracts;

namespace Access.Domain.Authentication;

public sealed class InvitationDelivery
{
    public TenantId TenantId { get; private set; }
    public Guid InvitationId { get; private set; }
    public string ProtectedToken { get; private set; } = null!;
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public int Attempts { get; private set; }

    private InvitationDelivery() { }

    public static InvitationDelivery Create(TenantId tenantId, Guid invitationId, string protectedToken, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(protectedToken))
            throw new ArgumentException("A protected token is required.", nameof(protectedToken));
        return new InvitationDelivery
        {
            TenantId = tenantId,
            InvitationId = invitationId,
            ProtectedToken = protectedToken,
            NextAttemptAt = now
        };
    }

    public void MarkDelivered(DateTimeOffset now)
    {
        DeliveredAt = now;
        ProtectedToken = string.Empty;
    }

    public void Retry(DateTimeOffset now)
    {
        Attempts++;
        NextAttemptAt = now.AddSeconds(Math.Min(3600, Math.Pow(2, Math.Min(Attempts, 12))));
    }
}
