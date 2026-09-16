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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM masterdata.parties WHERE id IN (SELECT id FROM crm.parties);");
        }
    }
}
