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

        builder.Property(f => f.FieldName).IsRequired();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_tenant_field_definitions_aggregate_type", "aggregate_type IN ('Party','Opportunity')");
            t.HasCheckConstraint("ck_tenant_field_definitions_field_type", "field_type IN ('text','number','boolean','date')");
        });

        // Unique on (tenant_id, aggregate_type, field_name) — not a generic EAV table set.
        builder.HasIndex(f => new { f.TenantId, f.AggregateType, f.FieldName }).IsUnique();
    }

    private static string FieldTypeToDb(TenantFieldValueType type) => type switch
    {
        TenantFieldValueType.Text => "text",
        TenantFieldValueType.Number => "number",
        TenantFieldValueType.Boolean => "boolean",
        TenantFieldValueType.Date => "date",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static TenantFieldValueType FieldTypeFromDb(string value) => value switch
    {
        "text" => TenantFieldValueType.Text,
        "number" => TenantFieldValueType.Number,
        "boolean" => TenantFieldValueType.Boolean,
        "date" => TenantFieldValueType.Date,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
