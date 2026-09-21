using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collaboration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE collaboration.calendar_entries ENABLE ROW LEVEL SECURITY;
                ALTER TABLE collaboration.calendar_entries FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON collaboration.calendar_entries
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);

                ALTER TABLE collaboration.idempotency_records ENABLE ROW LEVEL SECURITY;
                ALTER TABLE collaboration.idempotency_records FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON collaboration.idempotency_records
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);

                ALTER TABLE collaboration.outbox_messages ENABLE ROW LEVEL SECURITY;
                ALTER TABLE collaboration.outbox_messages FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON collaboration.outbox_messages
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY tenant_isolation ON collaboration.outbox_messages;
                ALTER TABLE collaboration.outbox_messages NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE collaboration.outbox_messages DISABLE ROW LEVEL SECURITY;

                DROP POLICY tenant_isolation ON collaboration.idempotency_records;
                ALTER TABLE collaboration.idempotency_records NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE collaboration.idempotency_records DISABLE ROW LEVEL SECURITY;

                DROP POLICY tenant_isolation ON collaboration.calendar_entries;
                ALTER TABLE collaboration.calendar_entries NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE collaboration.calendar_entries DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
