using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmSettingsConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "category",
                schema: "crm",
                table: "customer_needs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "crm",
                table: "customer_needs",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.CreateTable(
                name: "lost_reasons",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lost_reasons", x => x.id);
                    table.UniqueConstraint("ak_lost_reasons_tenant_id_id", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "opportunity_types",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opportunity_types", x => x.id);
                    table.UniqueConstraint("ak_opportunity_types_tenant_id_id", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "crm_settings",
                schema: "crm",
                columns: table => new
                {
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    default_pipeline_definition_id = table.Column<long>(type: "bigint", nullable: true),
                    opportunity_creation_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    default_opportunity_type_id = table.Column<long>(type: "bigint", nullable: true),
                    require_lost_reason = table.Column<bool>(type: "boolean", nullable: false),
                    require_won_line = table.Column<bool>(type: "boolean", nullable: false),
                    default_assignment_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    assignment_policy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    default_principal_issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    default_principal_subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    default_team_id = table.Column<long>(type: "bigint", nullable: true),
                    default_territory_id = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_crm_settings", x => x.tenant_id);
                    table.CheckConstraint("ck_crm_settings_default_principal_pair", "(default_principal_issuer IS NULL AND default_principal_subject IS NULL) OR (default_principal_issuer IS NOT NULL AND default_principal_subject IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_crm_settings_opportunity_types_tenant_id_default_opportunit",
                        columns: x => new { x.tenant_id, x.default_opportunity_type_id },
                        principalSchema: "crm",
                        principalTable: "opportunity_types",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_crm_settings_pipeline_definitions_tenant_id_default_pipelin",
                        columns: x => new { x.tenant_id, x.default_pipeline_definition_id },
                        principalSchema: "crm",
                        principalTable: "pipeline_definitions",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_crm_settings_tenant_id_default_opportunity_type_id",
                schema: "crm",
                table: "crm_settings",
                columns: new[] { "tenant_id", "default_opportunity_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_crm_settings_tenant_id_default_pipeline_definition_id",
                schema: "crm",
                table: "crm_settings",
                columns: new[] { "tenant_id", "default_pipeline_definition_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lost_reasons_tenant_id_key",
                schema: "crm",
                table: "lost_reasons",
                columns: new[] { "tenant_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_types_tenant_id_key",
                schema: "crm",
                table: "opportunity_types",
                columns: new[] { "tenant_id", "key" },
                unique: true);

            foreach (var table in new[] { "crm_settings", "lost_reasons", "opportunity_types" })
            {
                migrationBuilder.Sql($"ALTER TABLE crm.{table} ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE crm.{table} FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"CREATE POLICY tenant_isolation ON crm.{table} USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint) WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crm_settings",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "lost_reasons",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "opportunity_types",
                schema: "crm");

            migrationBuilder.DropColumn(
                name: "category",
                schema: "crm",
                table: "customer_needs");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "crm",
                table: "customer_needs");
        }
    }
}
