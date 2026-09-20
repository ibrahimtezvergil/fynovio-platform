using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Access.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthenticationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_credentials",
                schema: "identity",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    login_email_normalized = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    password_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account_credentials", x => x.account_id);
                    table.ForeignKey(
                        name: "fk_account_credentials_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "identity",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "auth_events",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    account_id = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    ip_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    detail = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "auth_sessions",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    active_tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    absolute_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    user_agent_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_auth_sessions_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "identity",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "auth_refresh_tokens",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rotated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auth_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_auth_refresh_tokens_auth_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "identity",
                        principalTable: "auth_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_credentials_login_email_normalized_unique",
                schema: "identity",
                table: "account_credentials",
                column: "login_email_normalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auth_events_account_id",
                schema: "identity",
                table: "auth_events",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_auth_events_correlation_id",
                schema: "identity",
                table: "auth_events",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_auth_events_occurred_at",
                schema: "identity",
                table: "auth_events",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_auth_events_session_id",
                schema: "identity",
                table: "auth_events",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_auth_refresh_tokens_session_id",
                schema: "identity",
                table: "auth_refresh_tokens",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_auth_refresh_tokens_token_hash_unique",
                schema: "identity",
                table: "auth_refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auth_sessions_account_id",
                schema: "identity",
                table: "auth_sessions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_auth_sessions_account_id_created_at",
                schema: "identity",
                table: "auth_sessions",
                columns: new[] { "account_id", "created_at" });

            // Add RLS policy for membership_self_view: allows authenticated users to view their own
            // membership rows across tenants via app.account_id GUC, complementing the existing
            // tenant_isolation policy (no UPDATE/DELETE path — SELECT-only permissive).
            migrationBuilder.Sql(
                @"CREATE POLICY membership_self_view ON identity.tenant_memberships FOR SELECT
                  USING (account_id = NULLIF(current_setting('app.account_id', true), '')::bigint);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the membership_self_view RLS policy
            migrationBuilder.Sql(
                @"DROP POLICY membership_self_view ON identity.tenant_memberships;");

            migrationBuilder.DropTable(
                name: "account_credentials",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "auth_events",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "auth_refresh_tokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "auth_sessions",
                schema: "identity");
        }
    }
}
