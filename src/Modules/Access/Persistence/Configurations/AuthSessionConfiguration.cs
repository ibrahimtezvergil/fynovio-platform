using Access.Domain.Authentication;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> builder)
    {
        builder.ToTable("auth_sessions", AccessDbContext.IdentitySchema);

        builder.HasKey(s => s.Id);

        builder.Property(s => s.AccountId)
            .IsRequired();

        builder.Property(s => s.ActiveTenantId)
            .HasConversion(
                v => v.HasValue ? v.Value.Value : (long?)null,
                v => v.HasValue ? new TenantId(v.Value) : null);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.LastUsedAt)
            .IsRequired();

        builder.Property(s => s.AbsoluteExpiresAt)
            .IsRequired();

        builder.Property(s => s.RevokedAt);

        builder.Property(s => s.RevokedReason)
            .HasMaxLength(50);

        builder.Property(s => s.UserAgentHash)
            .HasMaxLength(255);

        // Indexes for efficient lookups
        builder.HasIndex(s => s.AccountId)
            .HasDatabaseName("ix_auth_sessions_account_id");

        builder.HasIndex(s => new { s.AccountId, s.CreatedAt })
            .HasDatabaseName("ix_auth_sessions_account_id_created_at");

        // Foreign key to accounts
        builder.HasOne<Domain.Identity.Account>()
            .WithMany()
            .HasForeignKey(s => s.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
