using Collaboration.Domain;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Collaboration.Persistence.Configurations;

public sealed class CalendarEntryConfiguration : IEntityTypeConfiguration<CalendarEntry>
{
    public void Configure(EntityTypeBuilder<CalendarEntry> builder)
    {
        builder.ToTable("calendar_entries", table =>
        {
            table.HasCheckConstraint("ck_calendar_entries_title", "title = btrim(title) AND char_length(title) BETWEEN 1 AND 200 AND position(E'\\n' IN title) = 0 AND position(E'\\r' IN title) = 0");
            table.HasCheckConstraint("ck_calendar_entries_notes", "notes IS NULL OR char_length(notes) <= 4000");
            table.HasCheckConstraint("ck_calendar_entries_color", "color ~ '^#[0-9a-f]{6}$'");
            table.HasCheckConstraint("ck_calendar_entries_all_day_timing", "(all_day AND start_date IS NOT NULL AND end_date IS NOT NULL AND start_at IS NULL AND end_at IS NULL) OR (NOT all_day AND start_at IS NOT NULL AND start_date IS NULL AND end_date IS NULL)");
            table.HasCheckConstraint("ck_calendar_entries_end_at", "end_at IS NULL OR end_at > start_at");
            table.HasCheckConstraint("ck_calendar_entries_end_date", "end_date IS NULL OR end_date > start_date");
            table.HasCheckConstraint("ck_calendar_entries_link", "(link_bounded_context IS NULL AND link_entity_type IS NULL AND link_entity_id IS NULL) OR (link_bounded_context IS NOT NULL AND link_entity_type IS NOT NULL AND link_entity_id > 0)");
            table.HasCheckConstraint("ck_calendar_entries_link_identifiers", "(link_bounded_context IS NULL OR link_bounded_context ~ '^[a-z][a-z0-9_]*$') AND (link_entity_type IS NULL OR link_entity_type ~ '^[a-z][a-z0-9_]*$')");
        });
        builder.HasKey(entry => entry.Id);
        builder.HasAlternateKey(entry => new { entry.TenantId, entry.Id });
        builder.Property(entry => entry.TenantId).HasConversion(id => id.Value, value => new TenantId(value)).IsRequired();
        builder.Property(entry => entry.Title).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.Color).HasMaxLength(7).IsFixedLength().IsRequired();
        builder.Property(entry => entry.RowVersion).IsConcurrencyToken().IsRequired();
        builder.Ignore(entry => entry.Owner);
        builder.Ignore(entry => entry.Link);
        builder.HasIndex(entry => new { entry.TenantId, entry.OwnerPrincipalIssuer, entry.OwnerPrincipalSubject, entry.StartAt });
        builder.HasIndex(entry => new { entry.TenantId, entry.OwnerPrincipalIssuer, entry.OwnerPrincipalSubject, entry.StartDate });
        builder.HasIndex(entry => new { entry.TenantId, entry.OwnerPrincipalIssuer, entry.OwnerPrincipalSubject, entry.Id }).HasDatabaseName("ix_calendar_entries_owner_id");
    }
}
