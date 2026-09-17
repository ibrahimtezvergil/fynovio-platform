using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineStageActiveAndEntryFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "crm",
                table: "pipeline_stages",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_entry",
                schema: "crm",
                table: "pipeline_stages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ux_pipeline_stages_one_entry_per_version",
                schema: "crm",
                table: "pipeline_stages",
                columns: new[] { "tenant_id", "pipeline_definition_version_id" },
                unique: true,
                filter: "is_entry = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_pipeline_stages_one_entry_per_version",
                schema: "crm",
                table: "pipeline_stages");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "crm",
                table: "pipeline_stages");

            migrationBuilder.DropColumn(
                name: "is_entry",
                schema: "crm",
                table: "pipeline_stages");
        }
    }
}
