using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Access.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConstrainInvitationDeliveryState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_invitation_deliveries_state",
                schema: "access",
                table: "invitation_deliveries",
                sql: "attempts >= 0 AND ((delivered_at IS NULL AND length(protected_token) > 0) OR (delivered_at IS NOT NULL AND protected_token = ''))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_account_tokens_invited_role_shape",
                schema: "identity",
                table: "account_tokens",
                sql: "invited_role_key IS NULL OR (purpose = 'invite' AND length(trim(invited_role_key)) > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_invitation_deliveries_state",
                schema: "access",
                table: "invitation_deliveries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_account_tokens_invited_role_shape",
                schema: "identity",
                table: "account_tokens");
        }
    }
}
