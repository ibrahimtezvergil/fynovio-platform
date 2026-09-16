using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "pipeline_definition_version_id",
                schema: "crm",
                table: "opportunities",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "pipeline_stage_id",
                schema: "crm",
                table: "opportunities",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pipeline_definitions",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pipeline_definitions", x => x.id);
                    table.UniqueConstraint("ak_pipeline_definitions_tenant_id_id", x => new { x.tenant_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "pipeline_definition_versions",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    pipeline_definition_id = table.Column<long>(type: "bigint", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pipeline_definition_versions", x => x.id);
                    table.UniqueConstraint("ak_pipeline_definition_versions_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_pipeline_definition_versions_pipeline_definitions_pipeline_",
                        column: x => x.pipeline_definition_id,
                        principalSchema: "crm",
                        principalTable: "pipeline_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pipeline_definition_versions_pipeline_definitions_tenant_id",
                        columns: x => new { x.tenant_id, x.pipeline_definition_id },
                        principalSchema: "crm",
                        principalTable: "pipeline_definitions",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pipeline_stages",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    pipeline_definition_version_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pipeline_stages", x => x.id);
                    table.UniqueConstraint("ak_pipeline_stages_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_pipeline_stages_pipeline_definition_versions_pipeline_defin",
                        column: x => x.pipeline_definition_version_id,
                        principalSchema: "crm",
                        principalTable: "pipeline_definition_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pipeline_stages_pipeline_definition_versions_tenant_id_pipe",
                        columns: x => new { x.tenant_id, x.pipeline_definition_version_id },
                        principalSchema: "crm",
                        principalTable: "pipeline_definition_versions",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_tenant_id_pipeline_definition_version_id",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "pipeline_definition_version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_tenant_id_pipeline_stage_id",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "pipeline_stage_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_definition_versions_pipeline_definition_id",
                schema: "crm",
                table: "pipeline_definition_versions",
                column: "pipeline_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_definition_versions_tenant_id_pipeline_definition_",
                schema: "crm",
                table: "pipeline_definition_versions",
                columns: new[] { "tenant_id", "pipeline_definition_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_definitions_tenant_id_name",
                schema: "crm",
                table: "pipeline_definitions",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_stages_pipeline_definition_version_id",
                schema: "crm",
                table: "pipeline_stages",
                column: "pipeline_definition_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_stages_tenant_id_pipeline_definition_version_id_na",
                schema: "crm",
                table: "pipeline_stages",
                columns: new[] { "tenant_id", "pipeline_definition_version_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pipeline_stages_tenant_id_pipeline_definition_version_id_so",
                schema: "crm",
                table: "pipeline_stages",
                columns: new[] { "tenant_id", "pipeline_definition_version_id", "sort_order" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_opportunities_pipeline_definition_versions_tenant_id_pipeli",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "pipeline_definition_version_id" },
                principalSchema: "crm",
                principalTable: "pipeline_definition_versions",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_opportunities_pipeline_stages_tenant_id_pipeline_stage_id",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "pipeline_stage_id" },
                principalSchema: "crm",
                principalTable: "pipeline_stages",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_opportunities_pipeline_definition_versions_tenant_id_pipeli",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropForeignKey(
                name: "fk_opportunities_pipeline_stages_tenant_id_pipeline_stage_id",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropTable(
                name: "pipeline_stages",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "pipeline_definition_versions",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "pipeline_definitions",
                schema: "crm");

            migrationBuilder.DropIndex(
                name: "ix_opportunities_tenant_id_pipeline_definition_version_id",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropIndex(
                name: "ix_opportunities_tenant_id_pipeline_stage_id",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "pipeline_definition_version_id",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "pipeline_stage_id",
                schema: "crm",
                table: "opportunities");
        }
    }
}
