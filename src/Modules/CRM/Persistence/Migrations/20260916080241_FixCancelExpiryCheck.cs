using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixCancelExpiryCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunities_expiry_required_once_offered",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunities_expiry_required_once_offered",
                schema: "crm",
                table: "opportunities",
                sql: "status NOT IN ('offered','completed') OR expiry_date IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunities_expiry_required_once_offered",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunities_expiry_required_once_offered",
                schema: "crm",
                table: "opportunities",
                sql: "status = 'waiting' OR expiry_date IS NOT NULL");
        }
    }
}
