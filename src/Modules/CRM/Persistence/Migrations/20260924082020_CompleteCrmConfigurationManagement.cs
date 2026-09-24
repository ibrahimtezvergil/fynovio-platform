using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteCrmConfigurationManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                schema: "crm",
                table: "pipeline_stages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "crm",
                table: "pipeline_definitions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                schema: "crm",
                table: "pipeline_definitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "row_version",
                schema: "crm",
                table: "pipeline_definitions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "enforce_allowed_transitions",
                schema: "crm",
                table: "pipeline_definition_versions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "published_at",
                schema: "crm",
                table: "pipeline_definition_versions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "crm",
                table: "pipeline_definition_versions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Published");

            migrationBuilder.AddColumn<long>(
                name: "row_version",
                schema: "crm",
                table: "opportunity_types",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "row_version",
                schema: "crm",
                table: "lost_reasons",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "row_version",
                schema: "crm",
                table: "customer_needs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_pipeline_stages_tenant_id_pipeline_definition_version_id_id",
                schema: "crm",
                table: "pipeline_stages",
                columns: new[] { "tenant_id", "pipeline_definition_version_id", "id" });

            migrationBuilder.CreateTable(
                name: "pipeline_stage_transitions",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    pipeline_definition_version_id = table.Column<long>(type: "bigint", nullable: false),
                    from_stage_id = table.Column<long>(type: "bigint", nullable: false),
                    to_stage_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pipeline_stage_transitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_pipeline_stage_transitions_pipeline_definition_versions_ten",
                        columns: x => new { x.tenant_id, x.pipeline_definition_version_id },
                        principalSchema: "crm",
                        principalTable: "pipeline_definition_versions",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pipeline_stage_transitions_pipeline_stages_tenant_id_pipeli",
                        columns: x => new { x.tenant_id, x.pipeline_definition_version_id, x.from_stage_id },
                        principalSchema: "crm",
                        principalTable: "pipeline_stages",
                        principalColumns: new[] { "tenant_id", "pipeline_definition_version_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pipeline_stage_transitions_pipeline_stages_tenant_id_pipeli1",
                        columns: x => new { x.tenant_id, x.pipeline_definition_version_id, x.to_stage_id },
                        principalSchema: "crm",
                        principalTable: "pipeline_stages",
                        principalColumns: new[] { "tenant_id", "pipeline_definition_version_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_stage_transitions_tenant_id_pipeline_definition_ve",
                schema: "crm",
                table: "pipeline_stage_transitions",
                columns: new[] { "tenant_id", "pipeline_definition_version_id", "to_stage_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_stage_transitions_tenant_id_pipeline_definition_ve1",
                schema: "crm",
                table: "pipeline_stage_transitions",
                columns: new[] { "tenant_id", "pipeline_definition_version_id", "from_stage_id", "to_stage_id" },
                unique: true);

            migrationBuilder.Sql("ALTER TABLE crm.pipeline_stage_transitions ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE crm.pipeline_stage_transitions FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY tenant_isolation ON crm.pipeline_stage_transitions USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint) WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON crm.pipeline_stage_transitions;");
            migrationBuilder.Sql("ALTER TABLE crm.pipeline_stage_transitions NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE crm.pipeline_stage_transitions DISABLE ROW LEVEL SECURITY;");
            migrationBuilder.DropTable(
                name: "pipeline_stage_transitions",
                schema: "crm");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_pipeline_stages_tenant_id_pipeline_definition_version_id_id",
                schema: "crm",
                table: "pipeline_stages");

            migrationBuilder.DropColumn(
                name: "is_archived",
                schema: "crm",
                table: "pipeline_stages");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "crm",
                table: "pipeline_definitions");

            migrationBuilder.DropColumn(
                name: "is_archived",
                schema: "crm",
                table: "pipeline_definitions");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "crm",
                table: "pipeline_definitions");

            migrationBuilder.DropColumn(
                name: "enforce_allowed_transitions",
                schema: "crm",
                table: "pipeline_definition_versions");

            migrationBuilder.DropColumn(
                name: "published_at",
                schema: "crm",
                table: "pipeline_definition_versions");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "crm",
                table: "pipeline_definition_versions");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "crm",
                table: "opportunity_types");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "crm",
                table: "lost_reasons");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "crm",
                table: "customer_needs");
        }
    }
}
