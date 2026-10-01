using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SemanticCatalog.Domain;

namespace SemanticCatalog.Persistence.Configurations;

public sealed class CatalogViewDefinitionConfiguration : IEntityTypeConfiguration<CatalogViewDefinition>
{
    public void Configure(EntityTypeBuilder<CatalogViewDefinition> builder)
    {
        builder.ToTable("view_definitions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedOnAdd();

        builder.Property(v => v.TenantId).HasConversion(id => id.Value, value => new TenantId(value)).IsRequired();
        builder.Property(v => v.OwnerContext).HasMaxLength(32).IsRequired();
        builder.Property(v => v.ObjectType).HasMaxLength(32).IsRequired();
        builder.Property(v => v.Key).HasMaxLength(63).IsRequired();
        builder.Property(v => v.Name).HasMaxLength(CatalogViewDefinition.MaxNameLength).IsRequired();
        builder.Property(v => v.Kind).HasMaxLength(16).IsRequired().HasDefaultValue(CatalogViewDefinition.TableKind);
        builder.Property(v => v.ColumnsJson).HasColumnName("columns").HasColumnType("jsonb").IsRequired();
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(16).IsRequired().HasDefaultValue(FieldStatus.Active);
        builder.Property(v => v.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(v => v.RowVersion).IsRequired().HasDefaultValue(1L).IsConcurrencyToken();
        builder.Property(v => v.UpdatedAt).IsRequired().HasDefaultValueSql("now()");

        builder.Ignore(v => v.Columns);
        builder.Ignore(v => v.IsActive);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_view_definitions_owner", "owner_context = 'crm' AND object_type = 'opportunity'");
            t.HasCheckConstraint("ck_view_definitions_kind", "kind = 'table'");
            t.HasCheckConstraint("ck_view_definitions_key", "key ~ '^[a-z][a-z0-9_]{1,62}$'");
            t.HasCheckConstraint("ck_view_definitions_status", "status IN ('Active','Deprecated')");
            t.HasCheckConstraint("ck_view_definitions_sort_order", "sort_order BETWEEN 0 AND 10000");
            t.HasCheckConstraint("ck_view_definitions_columns", "jsonb_typeof(columns) = 'array' AND jsonb_array_length(columns) BETWEEN 1 AND 20");
        });

        builder.HasIndex(v => new { v.TenantId, v.OwnerContext, v.ObjectType, v.Key }).IsUnique();
    }
}

public sealed class DependencyEdgeConfiguration : IEntityTypeConfiguration<DependencyEdge>
{
    public void Configure(EntityTypeBuilder<DependencyEdge> builder)
    {
        builder.ToTable("dependency_edges");
        builder.HasKey(e => new { e.TenantId, e.FromKind, e.FromId, e.ToKind, e.ToId });
        builder.Property(e => e.TenantId).HasConversion(id => id.Value, value => new TenantId(value)).IsRequired();
        builder.Property(e => e.FromKind).HasMaxLength(16).IsRequired();
        builder.Property(e => e.ToKind).HasMaxLength(16).IsRequired();

        // An edge cannot outlive its view; a field is never hard-deleted, so the restrict is a guard, not a workflow.
        builder.HasOne<CatalogViewDefinition>().WithMany().HasForeignKey(e => e.FromId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CatalogFieldDefinition>().WithMany().HasForeignKey(e => e.ToId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_dependency_edges_from_kind", "from_kind = 'view'");
            t.HasCheckConstraint("ck_dependency_edges_to_kind", "to_kind = 'field'");
        });

        // "Which views use this field?" is the question the deprecate dialog asks.
        builder.HasIndex(e => new { e.TenantId, e.ToKind, e.ToId });
    }
}
