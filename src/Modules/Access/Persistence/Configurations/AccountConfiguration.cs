using Access.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Access.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts", AccessDbContext.IdentitySchema);

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Email).IsRequired();
        builder.Property(a => a.DisplayName).IsRequired();

        // No unique index on email — deliberately: email is profile data, not an identity
        // key (Contracts.PrincipalRef's own doc comment: "Do not use mutable email as
        // identity"). Uniqueness lives on external_identities(issuer, subject) instead.
    }
}
