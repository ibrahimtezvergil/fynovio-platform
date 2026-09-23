using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Access.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationDeliveryQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invitation_deliveries",
                schema: "access",
                columns: table => new
                {
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    protected_token = table.Column<string>(type: "text", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invitation_deliveries", x => new { x.tenant_id, x.invitation_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_invitation_deliveries_tenant_id_next_attempt_at",
                schema: "access",
                table: "invitation_deliveries",
                columns: new[] { "tenant_id", "next_attempt_at" },
                filter: "delivered_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invitation_deliveries",
                schema: "access");
        }
    }
}
