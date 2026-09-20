using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Access.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModuleEnablement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_role_assignments_source",
                schema: "access",
                table: "role_assignments");

            migrationBuilder.AddColumn<string>(
                name: "origin_module_key",
                schema: "access",
                table: "roles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "origin_version",
                schema: "access",
                table: "roles",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "source",
                schema: "access",
                table: "role_assignments",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AddColumn<string>(
                name: "origin_module_key",
                schema: "access",
                table: "permission_sets",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "origin_version",
                schema: "access",
                table: "permission_sets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tenant_module_enablements",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    module_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    template_version = table.Column<int>(type: "integer", nullable: false),
                    enabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_module_enablements", x => x.id);
                    table.CheckConstraint("ck_tenant_module_enablements_version", "template_version >= 1");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_roles_provenance",
                schema: "access",
                table: "roles",
                sql: "(origin_module_key IS NULL) = (origin_version IS NULL) AND (origin_module_key IS NULL OR origin = 'system_template') AND (origin_version IS NULL OR origin_version >= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_role_assignments_source",
                schema: "access",
                table: "role_assignments",
                sql: "source IN ('manual','bootstrap','module_enablement')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_permission_sets_provenance",
                schema: "access",
                table: "permission_sets",
                sql: "(origin_module_key IS NULL) = (origin_version IS NULL) AND (origin_module_key IS NULL OR origin = 'system_template') AND (origin_version IS NULL OR origin_version >= 1)");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_module_enablements_tenant_id_module_key",
                schema: "access",
                table: "tenant_module_enablements",
                columns: new[] { "tenant_id", "module_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_module_enablements",
                schema: "access");

            migrationBuilder.DropCheckConstraint(
                name: "ck_roles_provenance",
                schema: "access",
                table: "roles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_role_assignments_source",
                schema: "access",
                table: "role_assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_permission_sets_provenance",
                schema: "access",
                table: "permission_sets");

            migrationBuilder.DropColumn(
                name: "origin_module_key",
                schema: "access",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "origin_version",
                schema: "access",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "origin_module_key",
                schema: "access",
                table: "permission_sets");

            migrationBuilder.DropColumn(
                name: "origin_version",
                schema: "access",
                table: "permission_sets");

            migrationBuilder.AlterColumn<string>(
                name: "source",
                schema: "access",
                table: "role_assignments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(24)",
                oldMaxLength: 24);

            migrationBuilder.AddCheckConstraint(
                name: "ck_role_assignments_source",
                schema: "access",
                table: "role_assignments",
                sql: "source IN ('manual','bootstrap')");
        }
    }
}
