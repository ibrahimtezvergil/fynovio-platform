using Access.Domain.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("auth_refresh_tokens", AccessDbContext.IdentitySchema);

        builder.HasKey(t => t.Id);

        builder.Property(t => t.SessionId)
            .IsRequired();

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(t => t.IssuedAt)
            .IsRequired();

        builder.Property(t => t.ExpiresAt)
            .IsRequired();

        builder.Property(t => t.RotatedAt);

        builder.Property(t => t.ReplacedById);

        // Unique constraint on token hash
        builder.HasIndex(t => t.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_auth_refresh_tokens_token_hash_unique");

        // Index for efficient session lookups
        builder.HasIndex(t => t.SessionId)
            .HasDatabaseName("ix_auth_refresh_tokens_session_id");

        // Foreign key to auth_sessions
        builder.HasOne<AuthSession>()
            .WithMany()
            .HasForeignKey(t => t.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
