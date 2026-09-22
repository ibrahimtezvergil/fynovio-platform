using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TenantLifecycle.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTenantLifecycleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenant_lifecycle");

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                schema: "tenant_lifecycle",
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
                schema: "tenant_lifecycle",
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
                    causation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_profiles",
                schema: "tenant_lifecycle",
                columns: table => new
                {
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    tax_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    tax_office = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    timezone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_profiles", x => x.tenant_id);
                    table.CheckConstraint("ck_tenant_profiles_address", "address IS NULL OR (address = btrim(address) AND char_length(address) <= 500 AND address !~ '[\\u0001-\\u0009\\u000b-\\u001f\\u007f-\\u009f\\u2028\\u2029]')");
                    table.CheckConstraint("ck_tenant_profiles_currency_code", "currency_code ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_tenant_profiles_display_name", "btrim(display_name) = display_name AND display_name !~ '[\\u0001-\\u001f\\u007f-\\u009f\\u2028\\u2029]' AND char_length(display_name) BETWEEN 2 AND 120");
                    table.CheckConstraint("ck_tenant_profiles_email", "email IS NULL OR (email = btrim(email) AND email = lower(email) AND char_length(email) BETWEEN 1 AND 254)");
                    table.CheckConstraint("ck_tenant_profiles_legal_name", "legal_name IS NULL OR (btrim(legal_name) = legal_name AND legal_name !~ '[\\u0001-\\u001f\\u007f-\\u009f\\u2028\\u2029]' AND char_length(legal_name) BETWEEN 1 AND 160)");
                    table.CheckConstraint("ck_tenant_profiles_phone", "phone IS NULL OR (btrim(phone) = phone AND phone !~ '[\\u0001-\\u001f\\u007f-\\u009f\\u2028\\u2029]' AND char_length(phone) BETWEEN 1 AND 32)");
                    table.CheckConstraint("ck_tenant_profiles_tax_number", "tax_number IS NULL OR (btrim(tax_number) = tax_number AND tax_number !~ '[\\u0001-\\u001f\\u007f-\\u009f\\u2028\\u2029]' AND char_length(tax_number) BETWEEN 1 AND 32)");
                    table.CheckConstraint("ck_tenant_profiles_tax_office", "tax_office IS NULL OR (btrim(tax_office) = tax_office AND tax_office !~ '[\\u0001-\\u001f\\u007f-\\u009f\\u2028\\u2029]' AND char_length(tax_office) BETWEEN 1 AND 120)");
                    table.CheckConstraint("ck_tenant_profiles_tenant_id", "tenant_id > 0");
                    table.CheckConstraint("ck_tenant_profiles_timezone", "btrim(timezone) = timezone AND timezone !~ '[\\u0001-\\u001f\\u007f-\\u009f\\u2028\\u2029]' AND char_length(timezone) BETWEEN 1 AND 64");
                });

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_records_expires_at",
                schema: "tenant_lifecycle",
                table: "idempotency_records",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_id",
                schema: "tenant_lifecycle",
                table: "outbox_messages",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                schema: "tenant_lifecycle",
                table: "outbox_messages",
                column: "processed_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_tenant_id_aggregate_type_aggregate_id",
                schema: "tenant_lifecycle",
                table: "outbox_messages",
                columns: new[] { "tenant_id", "aggregate_type", "aggregate_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "tenant_lifecycle");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "tenant_lifecycle");

            migrationBuilder.DropTable(
                name: "tenant_profiles",
                schema: "tenant_lifecycle");
        }
    }
}
