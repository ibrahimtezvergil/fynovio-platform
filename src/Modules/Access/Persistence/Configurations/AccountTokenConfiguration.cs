using Access.Domain.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class AccountTokenConfiguration : IEntityTypeConfiguration<AccountToken>
{
    public void Configure(EntityTypeBuilder<AccountToken> builder)
    {
        builder.ToTable("account_tokens", AccessDbContext.IdentitySchema, table =>
        {
            table.HasCheckConstraint(
                "ck_account_tokens_purpose",
                "purpose IN ('invite', 'password_reset', 'password_setup')");
            table.HasCheckConstraint(
                "ck_account_tokens_invite_shape",
                "purpose <> 'invite' OR (tenant_id IS NOT NULL AND email_normalized IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_account_tokens_account_purposes_have_account",
                "purpose = 'invite' OR account_id IS NOT NULL");
        });

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Purpose).IsRequired().HasMaxLength(30);
        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(255);
        builder.Property(t => t.EmailNormalized).HasMaxLength(320);
        builder.Property(t => t.DisplayName).HasMaxLength(200);
        builder.Property(t => t.Locale).HasMaxLength(20);
        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();

        builder.HasIndex(t => t.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_account_tokens_token_hash_unique");

        builder.HasIndex(t => t.AccountId)
            .HasDatabaseName("ix_account_tokens_account_id");

        // Looking up the outstanding invitation(s) of one address in one tenant (re-invite revokes them).
        builder.HasIndex(t => new { t.EmailNormalized, t.TenantId })
            .HasFilter("purpose = 'invite' AND consumed_at IS NULL AND revoked_at IS NULL")
            .HasDatabaseName("ix_account_tokens_outstanding_invites");

        builder.HasOne<Access.Domain.Identity.Account>()
            .WithMany()
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
