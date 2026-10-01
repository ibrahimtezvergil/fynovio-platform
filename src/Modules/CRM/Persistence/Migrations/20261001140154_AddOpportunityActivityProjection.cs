using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityActivityProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consumed_events",
                schema: "crm",
                columns: table => new
                {
                    consumer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consumed_events", x => new { x.consumer, x.event_id });
                });

            migrationBuilder.CreateTable(
                name: "opportunity_activity",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    opportunity_id = table.Column<long>(type: "bigint", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    aggregate_version = table.Column<long>(type: "bigint", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opportunity_activity", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_activity_tenant_id_event_id",
                schema: "crm",
                table: "opportunity_activity",
                columns: new[] { "tenant_id", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_activity_tenant_id_opportunity_id_occurred_at",
                schema: "crm",
                table: "opportunity_activity",
                columns: new[] { "tenant_id", "opportunity_id", "occurred_at" });

            // RLS has no EF Core model representation (hand-written, as in AddOpportunityStageHistory).
            foreach (var table in new[] { "opportunity_activity", "consumed_events" })
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
            foreach (var table in new[] { "opportunity_activity", "consumed_events" })
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON crm.{table};");
                migrationBuilder.Sql($"ALTER TABLE crm.{table} NO FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE crm.{table} DISABLE ROW LEVEL SECURITY;");
            }

            migrationBuilder.DropTable(
                name: "consumed_events",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "opportunity_activity",
                schema: "crm");
        }
    }
}
