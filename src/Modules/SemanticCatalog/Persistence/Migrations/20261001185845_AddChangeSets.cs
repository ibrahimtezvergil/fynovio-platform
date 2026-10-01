using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SemanticCatalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalog_revisions",
                schema: "semantic",
                columns: table => new
                {
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_revisions", x => x.tenant_id);
                    table.CheckConstraint("ck_catalog_revisions_revision", "revision >= 0");
                });

            migrationBuilder.CreateTable(
                name: "change_sets",
                schema: "semantic",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    base_revision = table.Column<long>(type: "bigint", nullable: false),
                    published_revision = table.Column<long>(type: "bigint", nullable: true),
                    content_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    created_by_issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_by_subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_change_sets", x => x.id);
                    table.CheckConstraint("ck_change_sets_base_revision", "base_revision >= 0");
                    table.CheckConstraint("ck_change_sets_content_hash", "content_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_change_sets_published_revision", "(status IN ('Published','Activating','Active','ActivationFailed')) = (published_revision IS NOT NULL)");
                    table.CheckConstraint("ck_change_sets_source", "source IN ('Human')");
                    table.CheckConstraint("ck_change_sets_status", "status IN ('Draft','Validated','AwaitingApproval','Approved','Published','Activating','Active','ActivationFailed','Rejected','Discarded','Superseded')");
                });

            migrationBuilder.CreateTable(
                name: "change_set_items",
                schema: "semantic",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    change_set_id = table.Column<long>(type: "bigint", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    target_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    target_id = table.Column<long>(type: "bigint", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_change_set_items", x => x.id);
                    table.CheckConstraint("ck_change_set_items_kind", "target_kind IN ('field')");
                    table.CheckConstraint("ck_change_set_items_operation", "operation IN ('Create','Update','Deprecate','Reactivate')");
                    table.CheckConstraint("ck_change_set_items_target", "(operation = 'Create') = (target_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_change_set_items_change_sets_change_set_id",
                        column: x => x.change_set_id,
                        principalSchema: "semantic",
                        principalTable: "change_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_change_set_items_change_set_id_ordinal",
                schema: "semantic",
                table: "change_set_items",
                columns: new[] { "change_set_id", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_change_sets_tenant_id_published_revision",
                schema: "semantic",
                table: "change_sets",
                columns: new[] { "tenant_id", "published_revision" },
                unique: true,
                filter: "published_revision IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_change_sets_tenant_id_status",
                schema: "semantic",
                table: "change_sets",
                columns: new[] { "tenant_id", "status" });

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

        private static readonly string[] TenantScopedTables = ["change_sets", "change_set_items", "catalog_revisions"];

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
                name: "catalog_revisions",
                schema: "semantic");

            migrationBuilder.DropTable(
                name: "change_set_items",
                schema: "semantic");

            migrationBuilder.DropTable(
                name: "change_sets",
                schema: "semantic");
        }
    }
}
