using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Access.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableModuleEnablementRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RLS has no EF Core model representation — the one hand-written migration body
            // AGENTS.md permits. Same policy shape as EnableAccessRowLevelSecurity.
            migrationBuilder.Sql("ALTER TABLE access.tenant_module_enablements ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE access.tenant_module_enablements FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("""
                CREATE POLICY tenant_isolation ON access.tenant_module_enablements
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON access.tenant_module_enablements;");
            migrationBuilder.Sql("ALTER TABLE access.tenant_module_enablements NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE access.tenant_module_enablements DISABLE ROW LEVEL SECURITY;");
        }
    }
}
