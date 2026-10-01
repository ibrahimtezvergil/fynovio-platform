using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveFieldDefinitionsToSemanticCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // adr-semantic-catalog-changeset.md S-3: field definitions now live in the Semantic Catalog, whose migrations
            // run BEFORE CRM's. Refuse to drop rows the catalog does not hold (CRM migrated first on an existing
            // database) rather than lose them silently. Party rows are never copied (OD-6) and may be dropped.
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM crm.tenant_field_definitions WHERE aggregate_type = 'Opportunity') THEN
                        IF to_regclass('semantic.field_definitions') IS NULL THEN
                            RAISE EXCEPTION 'crm.tenant_field_definitions still holds field definitions but the semantic schema does not exist: apply the SemanticCatalog migrations before CRM.';
                        END IF;
                        IF EXISTS (
                            SELECT 1 FROM crm.tenant_field_definitions c
                            WHERE c.aggregate_type = 'Opportunity'
                              AND NOT EXISTS (SELECT 1 FROM semantic.field_definitions s WHERE s.id = c.id AND s.tenant_id = c.tenant_id AND s.key = c.field_name)) THEN
                            RAISE EXCEPTION 'crm.tenant_field_definitions holds field definitions that semantic.field_definitions lacks: apply the SemanticCatalog migrations before CRM.';
                        END IF;
                    END IF;
                END
                $guard$;
                """);

            migrationBuilder.DropTable(
                name: "tenant_field_definitions",
                schema: "crm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_field_definitions",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    aggregate_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    field_name = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    field_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    owner_scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Tenant"),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Active"),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_field_definitions", x => x.id);
                    table.CheckConstraint("ck_tenant_field_definitions_aggregate_type", "aggregate_type IN ('Party','Opportunity')");
                    table.CheckConstraint("ck_tenant_field_definitions_config_object", "jsonb_typeof(config) = 'object'");
                    table.CheckConstraint("ck_tenant_field_definitions_field_name", "field_name ~ '^[a-z][a-z0-9_]{1,62}$'");
                    table.CheckConstraint("ck_tenant_field_definitions_field_type", "field_type IN ('text','long_text','number','decimal','boolean','date','select','multi_select','email','phone','url')");
                    table.CheckConstraint("ck_tenant_field_definitions_owner_scope", "owner_scope = 'Tenant'");
                    table.CheckConstraint("ck_tenant_field_definitions_sort_order", "sort_order BETWEEN 0 AND 10000");
                    table.CheckConstraint("ck_tenant_field_definitions_status", "status IN ('Active','Deprecated')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_field_definitions_tenant_id_aggregate_type_field_name",
                schema: "crm",
                table: "tenant_field_definitions",
                columns: new[] { "tenant_id", "aggregate_type", "field_name" },
                unique: true);

            migrationBuilder.Sql("ALTER TABLE crm.tenant_field_definitions ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE crm.tenant_field_definitions FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("""
                CREATE POLICY tenant_isolation ON crm.tenant_field_definitions
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);
                """);
        }
    }
}
