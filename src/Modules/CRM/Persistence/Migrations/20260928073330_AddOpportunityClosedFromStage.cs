using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityClosedFromStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_opportunities_pipeline_stages_tenant_id_pipeline_definition",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.AddColumn<long>(
                name: "closed_from_stage_id",
                schema: "crm",
                table: "opportunities",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_tenant_id_pipeline_definition_version_id_clos",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "pipeline_definition_version_id", "closed_from_stage_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_opportunities_pipeline_stages_tenant_id_pipeline_definition",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "pipeline_definition_version_id", "closed_from_stage_id" },
                principalSchema: "crm",
                principalTable: "pipeline_stages",
                principalColumns: new[] { "tenant_id", "pipeline_definition_version_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_opportunities_pipeline_stages_tenant_id_pipeline_definition1",
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

            migrationBuilder.DropForeignKey(
                name: "fk_opportunities_pipeline_stages_tenant_id_pipeline_definition1",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropIndex(
                name: "ix_opportunities_tenant_id_pipeline_definition_version_id_clos",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "closed_from_stage_id",
                schema: "crm",
                table: "opportunities");

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
    }
}
