using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineStageKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                schema: "crm",
                table: "pipeline_stages",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "open");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pipeline_stages_kind",
                schema: "crm",
                table: "pipeline_stages",
                sql: "kind IN ('open','won','lost')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_pipeline_stages_kind",
                schema: "crm",
                table: "pipeline_stages");

            migrationBuilder.DropColumn(
                name: "kind",
                schema: "crm",
                table: "pipeline_stages");
        }
    }
}
