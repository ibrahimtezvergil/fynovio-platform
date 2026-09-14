using Access.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class ExternalIdentityConfiguration : IEntityTypeConfiguration<ExternalIdentity>
{
    public void Configure(EntityTypeBuilder<ExternalIdentity> builder)
    {
        builder.ToTable("external_identities", AccessDbContext.IdentitySchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Issuer).IsRequired();
        builder.Property(e => e.Subject).IsRequired();
        builder.Property(e => e.RawClaims).HasColumnType("jsonb");

        // Physical backing for PrincipalRef — issuer+subject together are the identity key.
        builder.HasIndex(e => new { e.Issuer, e.Subject }).IsUnique();

        // Same schema (identity -> identity) — a real FK is safe here regardless of how
        // Identity/Access eventually split, unlike role_assignments -> accounts below.
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
