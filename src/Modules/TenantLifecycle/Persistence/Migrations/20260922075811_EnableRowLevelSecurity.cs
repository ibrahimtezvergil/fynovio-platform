using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantLifecycle.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE tenant_lifecycle.tenant_profiles ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tenant_lifecycle.tenant_profiles FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_profiles_tenant_isolation ON tenant_lifecycle.tenant_profiles
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);

                ALTER TABLE tenant_lifecycle.idempotency_records ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tenant_lifecycle.idempotency_records FORCE ROW LEVEL SECURITY;
                CREATE POLICY idempotency_records_tenant_isolation ON tenant_lifecycle.idempotency_records
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);

                ALTER TABLE tenant_lifecycle.outbox_messages ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tenant_lifecycle.outbox_messages FORCE ROW LEVEL SECURITY;
                CREATE POLICY outbox_messages_tenant_isolation ON tenant_lifecycle.outbox_messages
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY tenant_profiles_tenant_isolation ON tenant_lifecycle.tenant_profiles;
                ALTER TABLE tenant_lifecycle.tenant_profiles NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE tenant_lifecycle.tenant_profiles DISABLE ROW LEVEL SECURITY;

                DROP POLICY idempotency_records_tenant_isolation ON tenant_lifecycle.idempotency_records;
                ALTER TABLE tenant_lifecycle.idempotency_records NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE tenant_lifecycle.idempotency_records DISABLE ROW LEVEL SECURITY;

                DROP POLICY outbox_messages_tenant_isolation ON tenant_lifecycle.outbox_messages;
                ALTER TABLE tenant_lifecycle.outbox_messages NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE tenant_lifecycle.outbox_messages DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
