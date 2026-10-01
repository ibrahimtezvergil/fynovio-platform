using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SemanticCatalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddViewDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_change_set_items_kind",
                schema: "semantic",
                table: "change_set_items");

            migrationBuilder.CreateTable(
                name: "view_definitions",
                schema: "semantic",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    owner_context = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    object_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    key = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "table"),
                    columns = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Active"),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_view_definitions", x => x.id);
                    table.CheckConstraint("ck_view_definitions_columns", "jsonb_typeof(columns) = 'array' AND jsonb_array_length(columns) BETWEEN 1 AND 20");
                    table.CheckConstraint("ck_view_definitions_key", "key ~ '^[a-z][a-z0-9_]{1,62}$'");
                    table.CheckConstraint("ck_view_definitions_kind", "kind = 'table'");
                    table.CheckConstraint("ck_view_definitions_owner", "owner_context = 'crm' AND object_type = 'opportunity'");
                    table.CheckConstraint("ck_view_definitions_sort_order", "sort_order BETWEEN 0 AND 10000");
                    table.CheckConstraint("ck_view_definitions_status", "status IN ('Active','Deprecated')");
                });

            migrationBuilder.CreateTable(
                name: "dependency_edges",
                schema: "semantic",
                columns: table => new
                {
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    from_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    from_id = table.Column<long>(type: "bigint", nullable: false),
                    to_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    to_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dependency_edges", x => new { x.tenant_id, x.from_kind, x.from_id, x.to_kind, x.to_id });
                    table.CheckConstraint("ck_dependency_edges_from_kind", "from_kind = 'view'");
                    table.CheckConstraint("ck_dependency_edges_to_kind", "to_kind = 'field'");
                    table.ForeignKey(
                        name: "fk_dependency_edges_field_definitions_to_id",
                        column: x => x.to_id,
                        principalSchema: "semantic",
                        principalTable: "field_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dependency_edges_view_definitions_from_id",
                        column: x => x.from_id,
                        principalSchema: "semantic",
                        principalTable: "view_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_change_set_items_kind",
                schema: "semantic",
                table: "change_set_items",
                sql: "target_kind IN ('field','view')");

            migrationBuilder.CreateIndex(
                name: "ix_dependency_edges_from_id",
                schema: "semantic",
                table: "dependency_edges",
                column: "from_id");

            migrationBuilder.CreateIndex(
                name: "ix_dependency_edges_tenant_id_to_kind_to_id",
                schema: "semantic",
                table: "dependency_edges",
                columns: new[] { "tenant_id", "to_kind", "to_id" });

            migrationBuilder.CreateIndex(
                name: "ix_dependency_edges_to_id",
                schema: "semantic",
                table: "dependency_edges",
                column: "to_id");

            migrationBuilder.CreateIndex(
                name: "ix_view_definitions_tenant_id_owner_context_object_type_key",
                schema: "semantic",
                table: "view_definitions",
                columns: new[] { "tenant_id", "owner_context", "object_type", "key" },
                unique: true);

            // RLS has no EF Core model representation (hand-written, as in every module's migration).
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"ALTER TABLE semantic.{table} ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE semantic.{table} FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"""
                    CREATE POLICY tenant_isolation ON semantic.{table}
                        USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                        WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);
                    """);
            }
        }

        private static readonly string[] TenantScopedTables = ["view_definitions", "dependency_edges"];

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON semantic.{table};");
                migrationBuilder.Sql($"ALTER TABLE semantic.{table} NO FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE semantic.{table} DISABLE ROW LEVEL SECURITY;");
            }

            migrationBuilder.DropTable(
                name: "dependency_edges",
                schema: "semantic");

            migrationBuilder.DropTable(
                name: "view_definitions",
                schema: "semantic");

            migrationBuilder.DropCheckConstraint(
                name: "ck_change_set_items_kind",
                schema: "semantic",
                table: "change_set_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_change_set_items_kind",
                schema: "semantic",
                table: "change_set_items",
                sql: "target_kind IN ('field')");
        }
    }
}
