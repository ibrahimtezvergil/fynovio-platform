using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropCrmParties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE crm.parties DISABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("DROP POLICY IF EXISTS parties_rls_tenant_isolation ON crm.parties;");
            migrationBuilder.Sql("DROP TABLE crm.parties;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE crm.parties (
                    id bigint NOT NULL DEFAULT nextval('crm.parties_id_seq'::regclass),
                    tenant_id integer NOT NULL,
                    name text NOT NULL,
                    surname text,
                    phone text,
                    email text,
                    creation_source text NOT NULL,
                    merged_into_party_id bigint,
                    custom_fields jsonb,
                    created_at timestamp with time zone NOT NULL,
                    updated_at timestamp with time zone NOT NULL,
                    PRIMARY KEY (id),
                    UNIQUE (tenant_id, id),
                    FOREIGN KEY (tenant_id, merged_into_party_id) REFERENCES crm.parties(tenant_id, id) ON DELETE RESTRICT,
                    CHECK (creation_source IN ('manual', 'ai_voice_capture'))
                );
                """);
            migrationBuilder.Sql("CREATE INDEX ix_parties_tenant_email ON crm.parties(tenant_id, email);");
            migrationBuilder.Sql("CREATE INDEX ix_parties_tenant_merged_into ON crm.parties(tenant_id, merged_into_party_id);");
            migrationBuilder.Sql("""
                ALTER TABLE crm.parties ENABLE ROW LEVEL SECURITY;
                CREATE POLICY parties_rls_tenant_isolation ON crm.parties
                    USING (tenant_id = CURRENT_SETTING('app.tenant_id')::integer)
                    WITH CHECK (tenant_id = CURRENT_SETTING('app.tenant_id')::integer);
                """);
        }
    }
}
