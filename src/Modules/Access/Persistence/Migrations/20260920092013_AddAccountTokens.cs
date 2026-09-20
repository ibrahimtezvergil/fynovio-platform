using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Access.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_tokens",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    account_id = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    email_normalized = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_account_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_tokens", x => x.id);
                    table.CheckConstraint("ck_account_tokens_account_purposes_have_account", "purpose = 'invite' OR account_id IS NOT NULL");
                    table.CheckConstraint("ck_account_tokens_invite_shape", "purpose <> 'invite' OR (tenant_id IS NOT NULL AND email_normalized IS NOT NULL)");
                    table.CheckConstraint("ck_account_tokens_purpose", "purpose IN ('invite', 'password_reset', 'password_setup')");
                    table.ForeignKey(
                        name: "fk_account_tokens_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "identity",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_tokens_account_id",
                schema: "identity",
                table: "account_tokens",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_account_tokens_outstanding_invites",
                schema: "identity",
                table: "account_tokens",
                columns: new[] { "email_normalized", "tenant_id" },
                filter: "purpose = 'invite' AND consumed_at IS NULL AND revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_account_tokens_token_hash_unique",
                schema: "identity",
                table: "account_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_tokens",
                schema: "identity");
        }
    }
}
