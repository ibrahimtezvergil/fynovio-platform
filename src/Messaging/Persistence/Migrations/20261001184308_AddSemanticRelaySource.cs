using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Messaging.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSemanticRelaySource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The catalog's outbox is the sixth relay source (adr-semantic-catalog-changeset.md S-8). Same hand-written
            // policy as the other five (EnableRowLevelSecurity); the column grants live in scripts/create-relay-role.sql.
            migrationBuilder.Sql("""
                CREATE POLICY relay_access ON semantic.outbox_messages
                    USING (current_user = 'fynovio_relay')
                    WITH CHECK (current_user = 'fynovio_relay');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS relay_access ON semantic.outbox_messages;");
        }
    }
}
