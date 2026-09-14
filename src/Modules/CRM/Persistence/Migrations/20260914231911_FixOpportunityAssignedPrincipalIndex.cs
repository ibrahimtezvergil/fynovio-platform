using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixOpportunityAssignedPrincipalIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_opportunities_tenant_id_assigned_principal_subject",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_tenant_assigned_principal",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "assigned_principal_issuer", "assigned_principal_subject" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_opportunities_tenant_assigned_principal",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_tenant_id_assigned_principal_subject",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "assigned_principal_subject" });
        }
    }
}
