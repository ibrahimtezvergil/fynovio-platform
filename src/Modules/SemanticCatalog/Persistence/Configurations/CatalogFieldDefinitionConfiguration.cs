using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SemanticCatalog.Domain;

namespace SemanticCatalog.Persistence.Configurations;

public sealed class CatalogFieldDefinitionConfiguration : IEntityTypeConfiguration<CatalogFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CatalogFieldDefinition> builder)
    {
        builder.ToTable("field_definitions");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedOnAdd();

        builder.Property(f => f.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(f => f.OwnerContext).HasMaxLength(32).IsRequired();
        builder.Property(f => f.ObjectType).HasMaxLength(32).IsRequired();
        builder.Property(f => f.Key).HasMaxLength(63).IsRequired();
        builder.Property(f => f.Label).HasMaxLength(CatalogFieldDefinition.MaxLabelLength).IsRequired();
        builder.Property(f => f.Type)
            .HasColumnName("field_type")
            .HasConversion(t => FieldTypeNames.ToName(t), t => FieldTypeNames.Parse(t))
            .HasMaxLength(16)
            .IsRequired();
        builder.Property(f => f.ConfigJson).HasColumnName("config").HasColumnType("jsonb").IsRequired().HasDefaultValueSql("'{}'::jsonb");
        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(16).IsRequired().HasDefaultValue(FieldStatus.Active);
        builder.Property(f => f.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(f => f.RowVersion).IsRequired().HasDefaultValue(1L).IsConcurrencyToken();
        builder.Property(f => f.UpdatedAt).IsRequired().HasDefaultValueSql("now()");

        builder.Ignore(f => f.Config);
        builder.Ignore(f => f.IsActive);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_field_definitions_owner", "owner_context = 'crm' AND object_type = 'opportunity'");
            t.HasCheckConstraint("ck_field_definitions_field_type",
                "field_type IN ('text','long_text','number','decimal','boolean','date','select','multi_select','email','phone','url')");
            t.HasCheckConstraint("ck_field_definitions_key", "key ~ '^[a-z][a-z0-9_]{1,62}$'");
            t.HasCheckConstraint("ck_field_definitions_status", "status IN ('Active','Deprecated')");
            t.HasCheckConstraint("ck_field_definitions_sort_order", "sort_order BETWEEN 0 AND 10000");
            t.HasCheckConstraint("ck_field_definitions_config_object", "jsonb_typeof(config) = 'object'");
        });

        builder.HasIndex(f => new { f.TenantId, f.OwnerContext, f.ObjectType, f.Key }).IsUnique();
    }
}
