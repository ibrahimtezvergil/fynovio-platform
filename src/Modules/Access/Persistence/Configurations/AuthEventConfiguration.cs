using Access.Domain.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class AuthEventConfiguration : IEntityTypeConfiguration<AuthEvent>
{
    public void Configure(EntityTypeBuilder<AuthEvent> builder)
    {
        builder.ToTable("auth_events", AccessDbContext.IdentitySchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.OccurredAt)
            .IsRequired();

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.AccountId);

        builder.Property(e => e.TenantId);

        builder.Property(e => e.SessionId);

        builder.Property(e => e.CorrelationId)
            .HasMaxLength(36);

        builder.Property(e => e.IpHash)
            .HasMaxLength(255);

        builder.Property(e => e.Outcome)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Detail)
            .HasColumnType("jsonb");

        // Indexes for efficient querying
        builder.HasIndex(e => e.OccurredAt)
            .HasDatabaseName("ix_auth_events_occurred_at");

        builder.HasIndex(e => e.AccountId)
            .HasDatabaseName("ix_auth_events_account_id");

        builder.HasIndex(e => e.SessionId)
            .HasDatabaseName("ix_auth_events_session_id");

        builder.HasIndex(e => e.CorrelationId)
            .HasDatabaseName("ix_auth_events_correlation_id");
    }
}
