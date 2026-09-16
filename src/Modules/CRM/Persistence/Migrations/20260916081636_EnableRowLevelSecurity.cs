using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RLS has no EF Core model representation — the one hand-written migration body
            // AGENTS.md permits ("Enforcement Scope", 2026-09-16).
            // NULLIF: once a session has used a transaction-local set_config, current_setting
            // returns '' (not NULL) after that transaction ends, and ''::bigint would error.
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"ALTER TABLE crm.{table} ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE crm.{table} FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"""
                    CREATE POLICY tenant_isolation ON crm.{table}
                        USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                        WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON crm.{table};");
                migrationBuilder.Sql($"ALTER TABLE crm.{table} NO FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE crm.{table} DISABLE ROW LEVEL SECURITY;");
            }
        }

        private static readonly string[] TenantScopedTables =
        [
            "parties",
            "customer_needs",
            "opportunities",
            "opportunity_lines",
            "opportunity_needs",
            "tenant_field_definitions",
            "outbox_messages",
            "idempotency_records",
            "evidence_records"
        ];
    }
}
