using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropOpportunityPartyForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_opportunities_parties_tenant_id_party_id",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.RenameColumn(
                name: "party_id",
                schema: "crm",
                table: "opportunities",
                newName: "party_ref_party_id");

            migrationBuilder.RenameIndex(
                name: "ix_opportunities_tenant_id_party_id",
                schema: "crm",
                table: "opportunities",
                newName: "ix_opportunities_tenant_id_party_ref_party_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunities_party_ref_party_id_positive",
                schema: "crm",
                table: "opportunities",
                sql: "party_ref_party_id > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunities_party_ref_party_id_positive",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.RenameColumn(
                name: "party_ref_party_id",
                schema: "crm",
                table: "opportunities",
                newName: "party_id");

            migrationBuilder.RenameIndex(
                name: "ix_opportunities_tenant_id_party_ref_party_id",
                schema: "crm",
                table: "opportunities",
                newName: "ix_opportunities_tenant_id_party_id");

            migrationBuilder.AddForeignKey(
                name: "fk_opportunities_parties_tenant_id_party_id",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "party_id" },
                principalSchema: "crm",
                principalTable: "parties",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
