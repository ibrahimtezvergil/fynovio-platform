using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceOpportunityStageVersionFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_opportunities_pipeline_stages_tenant_id_pipeline_stage_id",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_pipeline_stages_tenant_id_id",
                schema: "crm",
                table: "pipeline_stages");

            migrationBuilder.DropIndex(
                name: "ix_opportunities_tenant_id_pipeline_definition_version_id",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropIndex(
                name: "ix_opportunities_tenant_id_pipeline_stage_id",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_tenant_id_pipeline_definition_version_id_pipe",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "pipeline_definition_version_id", "pipeline_stage_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_opportunities_pipeline_stages_tenant_id_pipeline_definition",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "pipeline_definition_version_id", "pipeline_stage_id" },
                principalSchema: "crm",
                principalTable: "pipeline_stages",
                principalColumns: new[] { "tenant_id", "pipeline_definition_version_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_opportunities_pipeline_stages_tenant_id_pipeline_definition",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropIndex(
                name: "ix_opportunities_tenant_id_pipeline_definition_version_id_pipe",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_pipeline_stages_tenant_id_id",
                schema: "crm",
                table: "pipeline_stages",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_tenant_id_pipeline_definition_version_id",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "pipeline_definition_version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_tenant_id_pipeline_stage_id",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "pipeline_stage_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_opportunities_pipeline_stages_tenant_id_pipeline_stage_id",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "pipeline_stage_id" },
                principalSchema: "crm",
                principalTable: "pipeline_stages",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
