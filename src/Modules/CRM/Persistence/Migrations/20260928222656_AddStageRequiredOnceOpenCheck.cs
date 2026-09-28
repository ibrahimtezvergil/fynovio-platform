using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStageRequiredOnceOpenCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunities_stage_required_once_open",
                schema: "crm",
                table: "opportunities",
                sql: "status <> 'open' OR pipeline_stage_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunities_stage_required_once_open",
                schema: "crm",
                table: "opportunities");
        }
    }
}
