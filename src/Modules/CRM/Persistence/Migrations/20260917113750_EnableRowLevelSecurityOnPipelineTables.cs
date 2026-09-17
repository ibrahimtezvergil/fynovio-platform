using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurityOnPipelineTables : Migration
    {
        /// <summary>Closes a gap found by CRM_Phase1_Test_Coverage_Verification_Report.pdf
        /// (F-02): `AddPipelineTables` created `pipeline_definitions`,
        /// `pipeline_definition_versions` and `pipeline_stages` after `EnableRowLevelSecurity`
        /// already ran, so its hardcoded `TenantScopedTables` list never covered them — these
        /// three tenant-scoped tables shipped with no RLS at all, and `fynovio_app` (grants
        /// are `ON ALL TABLES IN SCHEMA crm`) had unfiltered cross-tenant read/write. Same
        /// hand-written-migration pattern as `EnableRowLevelSecurity` (AGENTS.md's named RLS
        /// exception; RLS has no EF Core model representation).</summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
            "pipeline_definitions",
            "pipeline_definition_versions",
            "pipeline_stages"
        ];
    }
}
