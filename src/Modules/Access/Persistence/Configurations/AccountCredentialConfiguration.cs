using Access.Domain.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class AccountCredentialConfiguration : IEntityTypeConfiguration<AccountCredential>
{
    public void Configure(EntityTypeBuilder<AccountCredential> builder)
    {
        builder.ToTable("account_credentials", AccessDbContext.IdentitySchema);

        builder.HasKey(c => c.AccountId);

        builder.Property(c => c.LoginEmailNormalized)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.PasswordHash)
            .IsRequired();

        builder.Property(c => c.PasswordChangedAt)
            .IsRequired();

        builder.Property(c => c.FailedAttempts)
            .IsRequired();

        builder.Property(c => c.LockedUntil);

        builder.Property(c => c.LastLoginAt);

        builder.Property(c => c.RowVersion)
            .IsRequired()
            .IsConcurrencyToken();

        // Unique constraint on normalized login email
        builder.HasIndex(c => c.LoginEmailNormalized)
            .IsUnique()
            .HasDatabaseName("ix_account_credentials_login_email_normalized_unique");

        // Foreign key to accounts (not tenant-scoped)
        builder.HasOne<Domain.Identity.Account>()
            .WithMany()
            .HasForeignKey(c => c.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
