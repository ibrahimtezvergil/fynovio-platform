using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Access.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableInvitationDeliveryRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE access.invitation_deliveries ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE access.invitation_deliveries FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("""
                CREATE POLICY tenant_isolation ON access.invitation_deliveries
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON access.invitation_deliveries;");
            migrationBuilder.Sql("ALTER TABLE access.invitation_deliveries NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE access.invitation_deliveries DISABLE ROW LEVEL SECURITY;");
        }
    }
}
