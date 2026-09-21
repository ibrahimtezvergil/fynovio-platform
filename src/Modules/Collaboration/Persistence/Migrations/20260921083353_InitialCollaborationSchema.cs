using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Collaboration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCollaborationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "collaboration");

            migrationBuilder.CreateTable(
                name: "calendar_entries",
                schema: "collaboration",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    owner_principal_issuer = table.Column<string>(type: "text", nullable: false),
                    owner_principal_subject = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    color = table.Column<string>(type: "character(7)", fixedLength: true, maxLength: 7, nullable: false),
                    all_day = table.Column<bool>(type: "boolean", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    link_bounded_context = table.Column<string>(type: "text", nullable: true),
                    link_entity_type = table.Column<string>(type: "text", nullable: true),
                    link_entity_id = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_entries", x => x.id);
                    table.UniqueConstraint("ak_calendar_entries_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_calendar_entries_all_day_timing", "(all_day AND start_date IS NOT NULL AND end_date IS NOT NULL AND start_at IS NULL AND end_at IS NULL) OR (NOT all_day AND start_at IS NOT NULL AND start_date IS NULL AND end_date IS NULL)");
                    table.CheckConstraint("ck_calendar_entries_color", "color ~ '^#[0-9a-f]{6}$'");
                    table.CheckConstraint("ck_calendar_entries_end_at", "end_at IS NULL OR end_at > start_at");
                    table.CheckConstraint("ck_calendar_entries_end_date", "end_date IS NULL OR end_date > start_date");
                    table.CheckConstraint("ck_calendar_entries_link", "(link_bounded_context IS NULL AND link_entity_type IS NULL AND link_entity_id IS NULL) OR (link_bounded_context IS NOT NULL AND link_entity_type IS NOT NULL AND link_entity_id > 0)");
                    table.CheckConstraint("ck_calendar_entries_link_identifiers", "(link_bounded_context IS NULL OR link_bounded_context ~ '^[a-z][a-z0-9_]*$') AND (link_entity_type IS NULL OR link_entity_type ~ '^[a-z][a-z0-9_]*$')");
                    table.CheckConstraint("ck_calendar_entries_notes", "notes IS NULL OR char_length(notes) <= 4000");
                    table.CheckConstraint("ck_calendar_entries_title", "title = btrim(title) AND char_length(title) BETWEEN 1 AND 200 AND position(E'\\n' IN title) = 0 AND position(E'\\r' IN title) = 0");
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                schema: "collaboration",
                columns: table => new
                {
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    principal_issuer = table.Column<string>(type: "text", nullable: false),
                    principal_subject = table.Column<string>(type: "text", nullable: false),
                    operation = table.Column<string>(type: "text", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    request_hash = table.Column<string>(type: "text", nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
                    response_payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_records", x => new { x.tenant_id, x.principal_issuer, x.principal_subject, x.operation, x.idempotency_key });
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "collaboration",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    aggregate_type = table.Column<string>(type: "text", nullable: false),
                    aggregate_id = table.Column<long>(type: "bigint", nullable: false),
                    aggregate_version = table.Column<long>(type: "bigint", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_entries_owner_id",
                schema: "collaboration",
                table: "calendar_entries",
                columns: new[] { "tenant_id", "owner_principal_issuer", "owner_principal_subject", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_entries_tenant_id_owner_principal_issuer_owner_pri",
                schema: "collaboration",
                table: "calendar_entries",
                columns: new[] { "tenant_id", "owner_principal_issuer", "owner_principal_subject", "start_at" });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_entries_tenant_id_owner_principal_issuer_owner_pri1",
                schema: "collaboration",
                table: "calendar_entries",
                columns: new[] { "tenant_id", "owner_principal_issuer", "owner_principal_subject", "start_date" });

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_records_expires_at",
                schema: "collaboration",
                table: "idempotency_records",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_id",
                schema: "collaboration",
                table: "outbox_messages",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                schema: "collaboration",
                table: "outbox_messages",
                column: "processed_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_tenant_id_aggregate_type_aggregate_id",
                schema: "collaboration",
                table: "outbox_messages",
                columns: new[] { "tenant_id", "aggregate_type", "aggregate_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_entries",
                schema: "collaboration");

            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "collaboration");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "collaboration");
        }
    }
}
