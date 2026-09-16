using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillMasterDataParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO masterdata.parties (id, tenant_id, party_type, name, surname, phone, email, merged_into_party_id, created_at, updated_at)
                SELECT id, tenant_id, 'organization', name, surname, phone, email, merged_into_party_id, created_at, updated_at
                FROM crm.parties;
                """);
            migrationBuilder.Sql("""
                SELECT setval(pg_get_serial_sequence('masterdata.parties', 'id'),
                    GREATEST((SELECT COALESCE(MAX(id), 0) FROM masterdata.parties), 1));
                """);
        }

        /// <summary>Reverses cleanly on its own (deletes exactly the rows this migration
        /// copied, matched against crm.parties' still-intact data). Rolling back further,
        /// past DropCrmParties — whose own Down() recreates an empty crm.parties table,
        /// not the original data (documented there as expected/fine, since no production
        /// tenants exist) — leaves this Down() with nothing to match against, so those
        /// rows are not removed. That's a consequence of DropCrmParties' already-accepted
        /// non-restoring Down(), not a new gap introduced here.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM masterdata.parties WHERE id IN (SELECT id FROM crm.parties);");
        }
    }
}
