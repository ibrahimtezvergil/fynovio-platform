using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <summary>Reconciles the EF model snapshot with the removal of CRM.Domain.Party
    /// (dead code once Opportunity moved to PartyRef): no schema change here. The
    /// physical crm.parties table's entire lifecycle — creation, the
    /// BackfillMasterDataParties data copy, and the DropCrmParties table drop — is
    /// already fully handled by earlier migrations. EF's diff would otherwise emit a
    /// DropTable("parties") here, which would fail on a fresh database (the table is
    /// already gone by this point in migration history) — so both directions are no-ops.</summary>
    public partial class RemoveCrmPartyEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
