using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityStageHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "opportunity_stage_history",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    opportunity_id = table.Column<long>(type: "bigint", nullable: false),
                    pipeline_definition_version_id = table.Column<long>(type: "bigint", nullable: false),
                    pipeline_stage_id = table.Column<long>(type: "bigint", nullable: false),
                    entered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    exited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opportunity_stage_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_opportunity_stage_history_opportunities_tenant_id_opportuni",
                        columns: x => new { x.tenant_id, x.opportunity_id },
                        principalSchema: "crm",
                        principalTable: "opportunities",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_opportunity_stage_history_pipeline_stages_tenant_id_pipelin",
                        columns: x => new { x.tenant_id, x.pipeline_definition_version_id, x.pipeline_stage_id },
                        principalSchema: "crm",
                        principalTable: "pipeline_stages",
                        principalColumns: new[] { "tenant_id", "pipeline_definition_version_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_stage_history_tenant_id_opportunity_id_entered_",
                schema: "crm",
                table: "opportunity_stage_history",
                columns: new[] { "tenant_id", "opportunity_id", "entered_at" });

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_stage_history_tenant_id_pipeline_definition_ver",
                schema: "crm",
                table: "opportunity_stage_history",
                columns: new[] { "tenant_id", "pipeline_definition_version_id", "pipeline_stage_id" });

            // Same hand-written pattern as EnableRowLevelSecurityOnPipelineTables/
            // AddCrmSettingsConfiguration (AGENTS.md's named RLS exception; RLS has no EF
            // Core model representation) — a new tenant-scoped table must get RLS in the
            // same migration that creates it, or `fynovio_app` has unfiltered cross-tenant
            // access to it from the moment it ships.
            migrationBuilder.Sql("ALTER TABLE crm.opportunity_stage_history ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE crm.opportunity_stage_history FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("""
                CREATE POLICY tenant_isolation ON crm.opportunity_stage_history
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON crm.opportunity_stage_history;");
            migrationBuilder.Sql("ALTER TABLE crm.opportunity_stage_history NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE crm.opportunity_stage_history DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.DropTable(
                name: "opportunity_stage_history",
                schema: "crm");
        }
    }
}
