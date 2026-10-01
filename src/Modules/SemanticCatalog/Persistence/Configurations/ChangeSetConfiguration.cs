using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SemanticCatalog.Domain;

namespace SemanticCatalog.Persistence.Configurations;

public sealed class ChangeSetConfiguration : IEntityTypeConfiguration<ChangeSet>
{
    public void Configure(EntityTypeBuilder<ChangeSet> builder)
    {
        builder.ToTable("change_sets");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedOnAdd();

        builder.Property(c => c.TenantId).HasConversion(id => id.Value, value => new TenantId(value)).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(c => c.Source).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(c => c.ContentHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(c => c.CreatedByIssuer).HasMaxLength(200).IsRequired();
        builder.Property(c => c.CreatedBySubject).HasMaxLength(200).IsRequired();
        builder.Property(c => c.FailureReason).HasMaxLength(200);
        builder.Property(c => c.RowVersion).IsRequired().HasDefaultValue(1L).IsConcurrencyToken();

        builder.Ignore(c => c.Transitions);
        builder.HasMany(c => c.Items).WithOne().HasForeignKey(i => i.ChangeSetId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_change_sets_status",
                "status IN ('Draft','Validated','AwaitingApproval','Approved','Published','Activating','Active','ActivationFailed','Rejected','Discarded','Superseded')");
            t.HasCheckConstraint("ck_change_sets_source", "source IN ('Human')");
            t.HasCheckConstraint("ck_change_sets_base_revision", "base_revision >= 0");
            // A set that was published carries the revision it produced; one that never was must not.
            t.HasCheckConstraint("ck_change_sets_published_revision",
                "(status IN ('Published','Activating','Active','ActivationFailed')) = (published_revision IS NOT NULL)");
            t.HasCheckConstraint("ck_change_sets_content_hash", "content_hash ~ '^[0-9a-f]{64}$'");
        });

        builder.HasIndex(c => new { c.TenantId, c.Status });
        builder.HasIndex(c => new { c.TenantId, c.PublishedRevision }).IsUnique().HasFilter("published_revision IS NOT NULL");
    }
}

public sealed class ChangeSetItemConfiguration : IEntityTypeConfiguration<ChangeSetItem>
{
    public void Configure(EntityTypeBuilder<ChangeSetItem> builder)
    {
        builder.ToTable("change_set_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedOnAdd();

        builder.Property(i => i.TenantId).HasConversion(id => id.Value, value => new TenantId(value)).IsRequired();
        builder.Property(i => i.TargetKind).HasMaxLength(16).IsRequired();
        builder.Property(i => i.Operation).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(i => i.PayloadJson).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Ignore(i => i.Content);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_change_set_items_kind", "target_kind IN ('field')");
            t.HasCheckConstraint("ck_change_set_items_operation", "operation IN ('Create','Update','Deprecate','Reactivate')");
            t.HasCheckConstraint("ck_change_set_items_target", "(operation = 'Create') = (target_id IS NULL)");
        });

        builder.HasIndex(i => new { i.ChangeSetId, i.Ordinal }).IsUnique();
    }
}

public sealed class CatalogRevisionConfiguration : IEntityTypeConfiguration<CatalogRevision>
{
    public void Configure(EntityTypeBuilder<CatalogRevision> builder)
    {
        builder.ToTable("catalog_revisions");
        builder.HasKey(r => r.TenantId);
        builder.Property(r => r.TenantId).HasConversion(id => id.Value, value => new TenantId(value)).ValueGeneratedNever();
        builder.Property(r => r.Revision).IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("ck_catalog_revisions_revision", "revision >= 0"));
    }
}
