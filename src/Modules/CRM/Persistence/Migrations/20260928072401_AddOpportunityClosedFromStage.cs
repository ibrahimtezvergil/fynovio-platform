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
            migrationBuilder.AddColumn<long>(
                name: "closed_from_stage_id",
                schema: "crm",
                table: "opportunities",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_closed_from_stage_id",
                schema: "crm",
                table: "opportunities",
                column: "closed_from_stage_id");

            migrationBuilder.AddForeignKey(
                name: "fk_opportunities_pipeline_stages_closed_from_stage_id",
                schema: "crm",
                table: "opportunities",
                column: "closed_from_stage_id",
                principalSchema: "crm",
                principalTable: "pipeline_stages",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_opportunities_pipeline_stages_closed_from_stage_id",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropIndex(
                name: "ix_opportunities_closed_from_stage_id",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "closed_from_stage_id",
                schema: "crm",
                table: "opportunities");
        }
    }
}
