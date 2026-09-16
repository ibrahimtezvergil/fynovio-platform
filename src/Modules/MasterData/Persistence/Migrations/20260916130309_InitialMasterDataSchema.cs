using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MasterData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialMasterDataSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "masterdata");

            migrationBuilder.CreateTable(
                name: "evidence_records",
                schema: "masterdata",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    aggregate_type = table.Column<string>(type: "text", nullable: false),
                    aggregate_id = table.Column<long>(type: "bigint", nullable: false),
                    aggregate_version = table.Column<long>(type: "bigint", nullable: false),
                    principal_issuer = table.Column<string>(type: "text", nullable: false),
                    principal_subject = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    detail = table.Column<string>(type: "jsonb", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_records",
                schema: "masterdata",
                columns: table => new
                {
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    principal_issuer = table.Column<string>(type: "text", nullable: false),
                    principal_subject = table.Column<string>(type: "text", nullable: false),
                    operation = table.Column<string>(type: "text", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    request_hash = table.Column<string>(type: "text", nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
                    response_payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_records", x => new { x.tenant_id, x.principal_issuer, x.principal_subject, x.operation, x.idempotency_key });
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "masterdata",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    aggregate_type = table.Column<string>(type: "text", nullable: false),
                    aggregate_id = table.Column<long>(type: "bigint", nullable: false),
                    aggregate_version = table.Column<long>(type: "bigint", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    causation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parties",
                schema: "masterdata",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    party_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    surname = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    merged_into_party_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parties", x => x.id);
                    table.UniqueConstraint("ak_parties_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_parties_party_type", "party_type IN ('person', 'organization')");
                    table.ForeignKey(
                        name: "fk_parties_parties_tenant_id_merged_into_party_id",
                        columns: x => new { x.tenant_id, x.merged_into_party_id },
                        principalSchema: "masterdata",
                        principalTable: "parties",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "party_external_identities",
                schema: "masterdata",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    party_id = table.Column<long>(type: "bigint", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    source_instance_ref = table.Column<string>(type: "text", nullable: false),
                    external_type = table.Column<string>(type: "text", nullable: true),
                    external_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    external_type_key = table.Column<string>(type: "text", nullable: true, computedColumnSql: "COALESCE(external_type, '')", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_party_external_identities", x => x.id);
                    table.ForeignKey(
                        name: "fk_party_external_identities_parties_tenant_id_party_id",
                        columns: x => new { x.tenant_id, x.party_id },
                        principalSchema: "masterdata",
                        principalTable: "parties",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "party_relationships",
                schema: "masterdata",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    from_party_id = table.Column<long>(type: "bigint", nullable: false),
                    to_party_id = table.Column<long>(type: "bigint", nullable: false),
                    relationship_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    job_title = table.Column<string>(type: "text", nullable: true),
                    work_email = table.Column<string>(type: "text", nullable: true),
                    work_phone = table.Column<string>(type: "text", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_party_relationships", x => x.id);
                    table.CheckConstraint("ck_party_relationships_ended_at_required_once_ended", "status <> 'ended' OR ended_at IS NOT NULL");
                    table.CheckConstraint("ck_party_relationships_status", "status IN ('active','ended')");
                    table.CheckConstraint("ck_party_relationships_type", "relationship_type IN ('works_for','branch_of')");
                    table.ForeignKey(
                        name: "fk_party_relationships_parties_tenant_id_from_party_id",
                        columns: x => new { x.tenant_id, x.from_party_id },
                        principalSchema: "masterdata",
                        principalTable: "parties",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_party_relationships_parties_tenant_id_to_party_id",
                        columns: x => new { x.tenant_id, x.to_party_id },
                        principalSchema: "masterdata",
                        principalTable: "parties",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_records_correlation_id",
                schema: "masterdata",
                table: "evidence_records",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_records_tenant_id_aggregate_type_aggregate_id",
                schema: "masterdata",
                table: "evidence_records",
                columns: new[] { "tenant_id", "aggregate_type", "aggregate_id" });

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_records_expires_at",
                schema: "masterdata",
                table: "idempotency_records",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_id",
                schema: "masterdata",
                table: "outbox_messages",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                schema: "masterdata",
                table: "outbox_messages",
                column: "processed_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_tenant_id_aggregate_type_aggregate_id",
                schema: "masterdata",
                table: "outbox_messages",
                columns: new[] { "tenant_id", "aggregate_type", "aggregate_id" });

            migrationBuilder.CreateIndex(
                name: "ix_parties_tenant_id_email",
                schema: "masterdata",
                table: "parties",
                columns: new[] { "tenant_id", "email" });

            migrationBuilder.CreateIndex(
                name: "ix_parties_tenant_id_merged_into_party_id",
                schema: "masterdata",
                table: "parties",
                columns: new[] { "tenant_id", "merged_into_party_id" });

            migrationBuilder.CreateIndex(
                name: "ix_party_external_identities_tenant_id_party_id",
                schema: "masterdata",
                table: "party_external_identities",
                columns: new[] { "tenant_id", "party_id" });

            migrationBuilder.CreateIndex(
                name: "ux_party_external_identities_tenant_source_type_external_id",
                schema: "masterdata",
                table: "party_external_identities",
                columns: new[] { "tenant_id", "source_instance_ref", "external_type_key", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_party_relationships_tenant_id_from_party_id",
                schema: "masterdata",
                table: "party_relationships",
                columns: new[] { "tenant_id", "from_party_id" });

            migrationBuilder.CreateIndex(
                name: "ix_party_relationships_tenant_id_to_party_id",
                schema: "masterdata",
                table: "party_relationships",
                columns: new[] { "tenant_id", "to_party_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence_records",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "party_external_identities",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "party_relationships",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "parties",
                schema: "masterdata");
        }
    }
}
