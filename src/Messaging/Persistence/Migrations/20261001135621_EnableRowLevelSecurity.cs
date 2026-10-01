using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Messaging.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RLS has no EF Core model representation — hand-written, as in every module's RLS migration.
            // event_deliveries: tenant isolation for the runtime role, plus the relay's cross-tenant access.
            migrationBuilder.Sql("ALTER TABLE messaging.event_deliveries ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE messaging.event_deliveries FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("""
                CREATE POLICY tenant_isolation ON messaging.event_deliveries
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);
                """);

            // The relay role (adr-event-consumption.md, E-1 (a), implementation note 2). A PUBLIC policy keyed on
            // current_user rather than `TO fynovio_relay`: the role is created by scripts/ after migrations run, and
            // an exact current_user match does not extend to roles that merely inherit from it. What the relay may
            // read or write is limited by its column grants (scripts/create-relay-role.sql), not by this policy.
            foreach (var table in RelayTables)
            {
                migrationBuilder.Sql($"""
                    CREATE POLICY relay_access ON {table}
                        USING (current_user = 'fynovio_relay')
                        WITH CHECK (current_user = 'fynovio_relay');
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in RelayTables)
                migrationBuilder.Sql($"DROP POLICY IF EXISTS relay_access ON {table};");

            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON messaging.event_deliveries;");
            migrationBuilder.Sql("ALTER TABLE messaging.event_deliveries NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE messaging.event_deliveries DISABLE ROW LEVEL SECURITY;");
        }

        private static readonly string[] RelayTables =
        [
            "messaging.event_deliveries",
            "crm.outbox_messages",
            "masterdata.outbox_messages",
            "access.outbox_messages",
            "collaboration.outbox_messages",
            "tenant_lifecycle.outbox_messages"
        ];
    }
}
