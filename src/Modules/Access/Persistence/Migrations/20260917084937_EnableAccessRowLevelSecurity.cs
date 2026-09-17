using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Access.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableAccessRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RLS has no EF Core model representation — the one hand-written migration
            // body AGENTS.md permits ("Enforcement Scope").
            foreach (var (schema, table) in TenantScopedTables)
            {
                migrationBuilder.Sql($"ALTER TABLE {schema}.{table} ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE {schema}.{table} FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"""
                    CREATE POLICY tenant_isolation ON {schema}.{table}
                        USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                        WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (schema, table) in TenantScopedTables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON {schema}.{table};");
                migrationBuilder.Sql($"ALTER TABLE {schema}.{table} NO FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE {schema}.{table} DISABLE ROW LEVEL SECURITY;");
            }
        }

        // "identity.accounts" and "identity.external_identities" are deliberately
        // excluded — same exemption as before (accounts is platform-global;
        // external_identities is account-scoped, not tenant-scoped). "access.actions"
        // is excluded — platform-owned catalog, no tenant_id column. All other
        // tenant-scoped tables (created in the RebuildAccessAuthorizationModel
        // migration, applied earlier in timestamp order) get RLS here — the
        // execution plan's Task 4/Task 5 split into two RLS migrations doesn't apply
        // since table creation for both was done in one migration during execution.
        private static readonly (string Schema, string Table)[] TenantScopedTables =
        [
            ("identity", "tenant_memberships"),
            ("access", "roles"),
            ("access", "permission_sets"),
            ("access", "permission_set_items"),
            ("access", "role_permission_sets"),
            ("access", "role_assignments"),
            ("access", "tenant_access_state"),
            ("access", "outbox_messages"),
            ("access", "idempotency_records"),
            ("access", "evidence_records")
        ];
    }
}
