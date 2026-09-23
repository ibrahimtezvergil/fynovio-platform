using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Access.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "invited_role_key",
                schema: "identity",
                table: "account_tokens",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "invited_role_key",
                schema: "identity",
                table: "account_tokens");
        }
    }
}
