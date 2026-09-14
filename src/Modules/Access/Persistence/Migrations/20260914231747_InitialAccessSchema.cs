using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Access.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAccessSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "access");

            migrationBuilder.CreateTable(
                name: "accounts",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    locale = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "external_identities",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    issuer = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    raw_claims = table.Column<string>(type: "jsonb", nullable: true),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_identities", x => x.id);
                    table.ForeignKey(
                        name: "fk_external_identities_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "identity",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_memberships",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    invited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    disabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_memberships", x => x.id);
                    table.CheckConstraint("ck_tenant_memberships_status", "status IN ('invited','active','disabled')");
                    table.ForeignKey(
                        name: "fk_tenant_memberships_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "identity",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_assignments",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    scope_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    scope_id = table.Column<long>(type: "bigint", nullable: true),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_assignments", x => x.id);
                    table.CheckConstraint("ck_role_assignments_scope_type", "scope_type IN ('tenant','organization_unit','network')");
                    table.ForeignKey(
                        name: "fk_role_assignments_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "access",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "access",
                columns: table => new
                {
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    permission_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "fk_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "access",
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "access",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_external_identities_account_id",
                schema: "identity",
                table: "external_identities",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_identities_issuer_subject",
                schema: "identity",
                table: "external_identities",
                columns: new[] { "issuer", "subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_permissions_key",
                schema: "access",
                table: "permissions",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_assignments_role_id",
                schema: "access",
                table: "role_assignments",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_assignments_tenant_id_account_id",
                schema: "access",
                table: "role_assignments",
                columns: new[] { "tenant_id", "account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_permission_id",
                schema: "access",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_memberships_account_id",
                schema: "identity",
                table: "tenant_memberships",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_memberships_tenant_id_account_id",
                schema: "identity",
                table: "tenant_memberships",
                columns: new[] { "tenant_id", "account_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_identities",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "role_assignments",
                schema: "access");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "access");

            migrationBuilder.DropTable(
                name: "tenant_memberships",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "access");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "access");

            migrationBuilder.DropTable(
                name: "accounts",
                schema: "identity");
        }
    }
}
