using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TenantLifecycle.Domain;

namespace TenantLifecycle.Persistence.Configurations;

public sealed class TenantProfileConfiguration : IEntityTypeConfiguration<TenantProfile>
{
    private const string SingleLine = @"btrim({0}) = {0} AND {0} !~ '[\u0001-\u001f\u007f-\u009f\u2028\u2029]'";

    public void Configure(EntityTypeBuilder<TenantProfile> builder)
    {
        builder.ToTable("tenant_profiles", table =>
        {
            table.HasCheckConstraint("ck_tenant_profiles_tenant_id", "tenant_id > 0");
            table.HasCheckConstraint("ck_tenant_profiles_display_name", string.Format(SingleLine, "display_name") + " AND char_length(display_name) BETWEEN 2 AND 120");
            table.HasCheckConstraint("ck_tenant_profiles_legal_name", "legal_name IS NULL OR (" + string.Format(SingleLine, "legal_name") + " AND char_length(legal_name) BETWEEN 1 AND 160)");
            table.HasCheckConstraint("ck_tenant_profiles_tax_number", "tax_number IS NULL OR (" + string.Format(SingleLine, "tax_number") + " AND char_length(tax_number) BETWEEN 1 AND 32)");
            table.HasCheckConstraint("ck_tenant_profiles_tax_office", "tax_office IS NULL OR (" + string.Format(SingleLine, "tax_office") + " AND char_length(tax_office) BETWEEN 1 AND 120)");
            table.HasCheckConstraint("ck_tenant_profiles_email", "email IS NULL OR (email = btrim(email) AND email = lower(email) AND char_length(email) BETWEEN 1 AND 254)");
            table.HasCheckConstraint("ck_tenant_profiles_phone", "phone IS NULL OR (" + string.Format(SingleLine, "phone") + " AND char_length(phone) BETWEEN 1 AND 32)");
            table.HasCheckConstraint("ck_tenant_profiles_address", @"address IS NULL OR (address = btrim(address) AND char_length(address) <= 500 AND address !~ '[\u0001-\u0009\u000b-\u001f\u007f-\u009f\u2028\u2029]')");
            table.HasCheckConstraint("ck_tenant_profiles_timezone", string.Format(SingleLine, "timezone") + " AND char_length(timezone) BETWEEN 1 AND 64");
            table.HasCheckConstraint("ck_tenant_profiles_currency_code", "currency_code ~ '^[A-Z]{3}$'");
        });

        builder.HasKey(profile => profile.TenantId);
        builder.Property(profile => profile.TenantId).HasConversion(id => id.Value, value => new TenantId(value)).ValueGeneratedNever();
        builder.Property(profile => profile.DisplayName).HasMaxLength(120).IsRequired();
        builder.Property(profile => profile.LegalName).HasMaxLength(160);
        builder.Property(profile => profile.TaxNumber).HasMaxLength(32);
        builder.Property(profile => profile.TaxOffice).HasMaxLength(120);
        builder.Property(profile => profile.Email).HasMaxLength(254);
        builder.Property(profile => profile.Phone).HasMaxLength(32);
        builder.Property(profile => profile.Address).HasMaxLength(500);
        builder.Property(profile => profile.Timezone).HasMaxLength(64).IsRequired();
        builder.Property(profile => profile.CurrencyCode).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(profile => profile.RowVersion).IsConcurrencyToken().IsRequired();
    }
}
