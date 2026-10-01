using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Messaging.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialMessagingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateTable(
                name: "consumer_registrations",
                schema: "messaging",
                columns: table => new
                {
                    consumer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    start_policy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consumer_registrations", x => x.consumer);
                });

            migrationBuilder.CreateTable(
                name: "event_deliveries",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    consumer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_schema = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    aggregate_type = table.Column<string>(type: "text", nullable: false),
                    aggregate_id = table.Column<long>(type: "bigint", nullable: false),
                    aggregate_version = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_deliveries", x => x.id);
                    table.CheckConstraint("ck_event_deliveries_status", "status IN ('pending', 'processing', 'delivered', 'skipped', 'dead')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_event_deliveries_consumer_event_id",
                schema: "messaging",
                table: "event_deliveries",
                columns: new[] { "consumer", "event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_deliveries_consumer_tenant_id_source_schema_aggregate",
                schema: "messaging",
                table: "event_deliveries",
                columns: new[] { "consumer", "tenant_id", "source_schema", "aggregate_type", "aggregate_id", "aggregate_version", "source_id" });

            migrationBuilder.CreateIndex(
                name: "ix_event_deliveries_status_next_attempt_at",
                schema: "messaging",
                table: "event_deliveries",
                columns: new[] { "status", "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consumer_registrations",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "event_deliveries",
                schema: "messaging");
        }
    }
}
