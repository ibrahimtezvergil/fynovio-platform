using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SemanticCatalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSemanticCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "semantic");

            migrationBuilder.CreateTable(
                name: "evidence_records",
                schema: "semantic",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    aggregate_type = table.Column<string>(type: "text", nullable: false),
                    aggregate_id = table.Column<long>(type: "bigint", nullable: false),
                    aggregate_version = table.Column<long>(type: "bigint", nullable: false),
                    principal_issuer = table.Column<string>(type: "text", nullable: false),
                    principal_subject = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    detail = table.Column<string>(type: "jsonb", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "field_definitions",
                schema: "semantic",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    owner_context = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    object_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    key = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    field_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Active"),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_field_definitions", x => x.id);
                    table.CheckConstraint("ck_field_definitions_config_object", "jsonb_typeof(config) = 'object'");
                    table.CheckConstraint("ck_field_definitions_field_type", "field_type IN ('text','long_text','number','decimal','boolean','date','select','multi_select','email','phone','url')");
                    table.CheckConstraint("ck_field_definitions_key", "key ~ '^[a-z][a-z0-9_]{1,62}$'");
                    table.CheckConstraint("ck_field_definitions_owner", "owner_context = 'crm' AND object_type = 'opportunity'");
                    table.CheckConstraint("ck_field_definitions_sort_order", "sort_order BETWEEN 0 AND 10000");
                    table.CheckConstraint("ck_field_definitions_status", "status IN ('Active','Deprecated')");
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                schema: "semantic",
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
                schema: "semantic",
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

            migrationBuilder.CreateIndex(
                name: "ix_evidence_records_correlation_id",
                schema: "semantic",
                table: "evidence_records",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_records_tenant_id_aggregate_type_aggregate_id",
                schema: "semantic",
                table: "evidence_records",
                columns: new[] { "tenant_id", "aggregate_type", "aggregate_id" });

            migrationBuilder.CreateIndex(
                name: "ix_field_definitions_tenant_id_owner_context_object_type_key",
                schema: "semantic",
                table: "field_definitions",
                columns: new[] { "tenant_id", "owner_context", "object_type", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_records_expires_at",
                schema: "semantic",
                table: "idempotency_records",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_id",
                schema: "semantic",
                table: "outbox_messages",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                schema: "semantic",
                table: "outbox_messages",
                column: "processed_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_tenant_id_aggregate_type_aggregate_id",
                schema: "semantic",
                table: "outbox_messages",
                columns: new[] { "tenant_id", "aggregate_type", "aggregate_id" });

            // adr-semantic-catalog-changeset.md S-3: the catalog is migrated BEFORE CRM. When CRM's old definition
            // table still exists (an existing database), its Opportunity rows are copied with their ids, so every id in
            // the API and the web UI stays valid, and the sequence is advanced past them. On a fresh database the table
            // does not exist yet and there is nothing to copy. Done before RLS is enabled: the migration role is the
            // schema owner and a FORCE policy would otherwise apply to this insert. Party rows cannot exist through the
            // API (OD-6) and are not copied.
            migrationBuilder.Sql("""
                DO $copy$
                BEGIN
                    IF to_regclass('crm.tenant_field_definitions') IS NOT NULL THEN
                        INSERT INTO semantic.field_definitions
                            (id, tenant_id, owner_context, object_type, key, label, field_type, is_required, config, status, sort_order, row_version, created_at, updated_at)
                        SELECT id, tenant_id, 'crm', 'opportunity', field_name, label, field_type, is_required, config, status, sort_order, row_version, created_at, updated_at
                        FROM crm.tenant_field_definitions
                        WHERE aggregate_type = 'Opportunity';

                        PERFORM setval(
                            pg_get_serial_sequence('semantic.field_definitions', 'id'),
                            GREATEST((SELECT COALESCE(MAX(id), 0) FROM crm.tenant_field_definitions), 1),
                            (SELECT COUNT(*) FROM crm.tenant_field_definitions) > 0);
                    END IF;
                END
                $copy$;
                """);

            // RLS has no EF Core model representation (hand-written, as in the other modules).
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"ALTER TABLE semantic.{table} ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE semantic.{table} FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"""
                    CREATE POLICY tenant_isolation ON semantic.{table}
                        USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                        WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);
                    """);
            }
        }

        private static readonly string[] TenantScopedTables = ["field_definitions", "idempotency_records", "evidence_records", "outbox_messages"];

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON semantic.{table};");
                migrationBuilder.Sql($"ALTER TABLE semantic.{table} NO FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE semantic.{table} DISABLE ROW LEVEL SECURITY;");
            }

            migrationBuilder.DropTable(
                name: "evidence_records",
                schema: "semantic");

            migrationBuilder.DropTable(
                name: "field_definitions",
                schema: "semantic");

            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "semantic");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "semantic");
        }
    }
}
