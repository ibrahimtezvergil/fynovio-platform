using Contracts;
using CRM.Customization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CRM.Persistence.Configurations;

public sealed class TenantFieldDefinitionConfiguration : IEntityTypeConfiguration<TenantFieldDefinition>
{
    public void Configure(EntityTypeBuilder<TenantFieldDefinition> builder)
    {
        builder.ToTable("tenant_field_definitions");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedOnAdd();

        builder.Property(f => f.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(f => f.AggregateType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(f => f.FieldType)
            .HasConversion(t => FieldTypeToDb(t), t => FieldTypeFromDb(t))
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(f => f.FieldName).HasMaxLength(63).IsRequired();
        builder.Property(f => f.Label).HasMaxLength(TenantFieldDefinition.MaxLabelLength).IsRequired().HasDefaultValue(string.Empty);
        builder.Property(f => f.ConfigJson).HasColumnName("config").HasColumnType("jsonb").IsRequired().HasDefaultValueSql("'{}'::jsonb");
        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(16).IsRequired().HasDefaultValue(TenantFieldStatus.Active);
        builder.Property(f => f.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(f => f.OwnerScope).HasConversion<string>().HasMaxLength(16).IsRequired().HasDefaultValue(TenantFieldOwnerScope.Tenant);
        builder.Property(f => f.RowVersion).IsRequired().HasDefaultValue(1L).IsConcurrencyToken();
        builder.Property(f => f.UpdatedAt).IsRequired().HasDefaultValueSql("now()");

        builder.Ignore(f => f.Config);
        builder.Ignore(f => f.IsActive);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_tenant_field_definitions_aggregate_type", "aggregate_type IN ('Party','Opportunity')");
            t.HasCheckConstraint("ck_tenant_field_definitions_field_type",
                "field_type IN ('text','long_text','number','decimal','boolean','date','select','multi_select','email','phone','url')");
            t.HasCheckConstraint("ck_tenant_field_definitions_field_name", "field_name ~ '^[a-z][a-z0-9_]{1,62}$'");
            t.HasCheckConstraint("ck_tenant_field_definitions_status", "status IN ('Active','Deprecated')");
            t.HasCheckConstraint("ck_tenant_field_definitions_owner_scope", "owner_scope = 'Tenant'");
            t.HasCheckConstraint("ck_tenant_field_definitions_sort_order", "sort_order BETWEEN 0 AND 10000");
            t.HasCheckConstraint("ck_tenant_field_definitions_config_object", "jsonb_typeof(config) = 'object'");
        });

        // Unique on (tenant_id, aggregate_type, field_name) — not a generic EAV table set.
        builder.HasIndex(f => new { f.TenantId, f.AggregateType, f.FieldName }).IsUnique();
    }

    private static string FieldTypeToDb(TenantFieldValueType type) => type switch
    {
        TenantFieldValueType.Text => "text",
        TenantFieldValueType.LongText => "long_text",
        TenantFieldValueType.Number => "number",
        TenantFieldValueType.Decimal => "decimal",
        TenantFieldValueType.Boolean => "boolean",
        TenantFieldValueType.Date => "date",
        TenantFieldValueType.Select => "select",
        TenantFieldValueType.MultiSelect => "multi_select",
        TenantFieldValueType.Email => "email",
        TenantFieldValueType.Phone => "phone",
        TenantFieldValueType.Url => "url",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static TenantFieldValueType FieldTypeFromDb(string value) => value switch
    {
        "text" => TenantFieldValueType.Text,
        "long_text" => TenantFieldValueType.LongText,
        "number" => TenantFieldValueType.Number,
        "decimal" => TenantFieldValueType.Decimal,
        "boolean" => TenantFieldValueType.Boolean,
        "date" => TenantFieldValueType.Date,
        "select" => TenantFieldValueType.Select,
        "multi_select" => TenantFieldValueType.MultiSelect,
        "email" => TenantFieldValueType.Email,
        "phone" => TenantFieldValueType.Phone,
        "url" => TenantFieldValueType.Url,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
