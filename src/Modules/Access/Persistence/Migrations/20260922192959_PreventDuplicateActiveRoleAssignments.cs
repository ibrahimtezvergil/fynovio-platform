using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Access.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreventDuplicateActiveRoleAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_role_assignments_active_role",
                schema: "access",
                table: "role_assignments",
                columns: new[] { "tenant_id", "account_id", "role_id" },
                unique: true,
                filter: "valid_to IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_role_assignments_active_role",
                schema: "access",
                table: "role_assignments");
        }
    }
}
