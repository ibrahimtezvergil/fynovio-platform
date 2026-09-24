using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApplyCrmOpportunityDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "opportunity_type_id",
                schema: "crm",
                table: "opportunities",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_tenant_id_opportunity_type_id",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "opportunity_type_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_opportunities_opportunity_types_tenant_id_opportunity_type_",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "opportunity_type_id" },
                principalSchema: "crm",
                principalTable: "opportunity_types",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_opportunities_opportunity_types_tenant_id_opportunity_type_",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropIndex(
                name: "ix_opportunities_tenant_id_opportunity_type_id",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "opportunity_type_id",
                schema: "crm",
                table: "opportunities");
        }
    }
}
