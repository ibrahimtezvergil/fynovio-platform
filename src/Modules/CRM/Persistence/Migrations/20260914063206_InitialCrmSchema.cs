using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCrmSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "crm");

            migrationBuilder.CreateTable(
                name: "customer_needs",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    average_price = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_needs", x => x.id);
                    table.UniqueConstraint("ak_customer_needs_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_customer_needs_average_price_non_negative", "average_price >= 0");
                });

            migrationBuilder.CreateTable(
                name: "evidence_records",
                schema: "crm",
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
                schema: "crm",
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
                schema: "crm",
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
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    surname = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    creation_source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    merged_into_party_id = table.Column<long>(type: "bigint", nullable: true),
                    custom_fields = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parties", x => x.id);
                    table.UniqueConstraint("ak_parties_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_parties_creation_source", "creation_source IN ('manual', 'ai_voice_capture')");
                    table.ForeignKey(
                        name: "fk_parties_parties_tenant_id_merged_into_party_id",
                        columns: x => new { x.tenant_id, x.merged_into_party_id },
                        principalSchema: "crm",
                        principalTable: "parties",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_field_definitions",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    field_name = table.Column<string>(type: "text", nullable: false),
                    field_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_field_definitions", x => x.id);
                    table.CheckConstraint("ck_tenant_field_definitions_aggregate_type", "aggregate_type IN ('Party','Opportunity')");
                    table.CheckConstraint("ck_tenant_field_definitions_field_type", "field_type IN ('text','number','boolean','date')");
                });

            migrationBuilder.CreateTable(
                name: "opportunities",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    party_id = table.Column<long>(type: "bigint", nullable: false),
                    assigned_principal_issuer = table.Column<string>(type: "text", nullable: false),
                    assigned_principal_subject = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    cancel_reason = table.Column<string>(type: "text", nullable: true),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    estimated_amount = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(19,4)", nullable: true),
                    expiry_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    offer_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sale_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    custom_fields = table.Column<string>(type: "jsonb", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opportunities", x => x.id);
                    table.UniqueConstraint("ak_opportunities_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_opportunities_cancel_fields_required_once_canceled", "status <> 'canceled' OR (cancel_date IS NOT NULL AND cancel_reason IS NOT NULL)");
                    table.CheckConstraint("ck_opportunities_estimated_amount_non_negative", "estimated_amount >= 0");
                    table.CheckConstraint("ck_opportunities_expiry_required_once_offered", "status = 'waiting' OR expiry_date IS NOT NULL");
                    table.CheckConstraint("ck_opportunities_sale_date_required_once_completed", "status <> 'completed' OR sale_date IS NOT NULL");
                    table.CheckConstraint("ck_opportunities_status", "status IN ('waiting','offered','completed','canceled')");
                    table.ForeignKey(
                        name: "fk_opportunities_parties_tenant_id_party_id",
                        columns: x => new { x.tenant_id, x.party_id },
                        principalSchema: "crm",
                        principalTable: "parties",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "opportunity_lines",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    opportunity_id = table.Column<long>(type: "bigint", nullable: false),
                    product_ref_bounded_context = table.Column<string>(type: "text", nullable: false),
                    product_ref_entity_type = table.Column<string>(type: "text", nullable: false),
                    product_ref_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(19,2)", nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(19,4)", nullable: true),
                    is_optional = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_canceled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    cancel_reason = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opportunity_lines", x => x.id);
                    table.CheckConstraint("ck_opportunity_lines_quantity_positive", "quantity > 0");
                    table.CheckConstraint("ck_opportunity_lines_unit_price_non_negative", "unit_price >= 0");
                    table.ForeignKey(
                        name: "fk_opportunity_lines_opportunities_tenant_id_opportunity_id",
                        columns: x => new { x.tenant_id, x.opportunity_id },
                        principalSchema: "crm",
                        principalTable: "opportunities",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "opportunity_needs",
                schema: "crm",
                columns: table => new
                {
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    opportunity_id = table.Column<long>(type: "bigint", nullable: false),
                    customer_need_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opportunity_needs", x => new { x.tenant_id, x.opportunity_id, x.customer_need_id });
                    table.ForeignKey(
                        name: "fk_opportunity_needs_customer_needs_tenant_id_customer_need_id",
                        columns: x => new { x.tenant_id, x.customer_need_id },
                        principalSchema: "crm",
                        principalTable: "customer_needs",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_opportunity_needs_opportunities_tenant_id_opportunity_id",
                        columns: x => new { x.tenant_id, x.opportunity_id },
                        principalSchema: "crm",
                        principalTable: "opportunities",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_records_correlation_id",
                schema: "crm",
                table: "evidence_records",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_records_tenant_id_aggregate_type_aggregate_id",
                schema: "crm",
                table: "evidence_records",
                columns: new[] { "tenant_id", "aggregate_type", "aggregate_id" });

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_records_expires_at",
                schema: "crm",
                table: "idempotency_records",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_tenant_id_assigned_principal_subject",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "assigned_principal_subject" });

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_tenant_id_created_at",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_tenant_id_party_id",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "party_id" });

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_tenant_id_status",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_lines_tenant_id_opportunity_id",
                schema: "crm",
                table: "opportunity_lines",
                columns: new[] { "tenant_id", "opportunity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_opportunity_needs_tenant_id_customer_need_id",
                schema: "crm",
                table: "opportunity_needs",
                columns: new[] { "tenant_id", "customer_need_id" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_id",
                schema: "crm",
                table: "outbox_messages",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                schema: "crm",
                table: "outbox_messages",
                column: "processed_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_tenant_id_aggregate_type_aggregate_id",
                schema: "crm",
                table: "outbox_messages",
                columns: new[] { "tenant_id", "aggregate_type", "aggregate_id" });

            migrationBuilder.CreateIndex(
                name: "ix_parties_tenant_id_email",
                schema: "crm",
                table: "parties",
                columns: new[] { "tenant_id", "email" });

            migrationBuilder.CreateIndex(
                name: "ix_parties_tenant_id_merged_into_party_id",
                schema: "crm",
                table: "parties",
                columns: new[] { "tenant_id", "merged_into_party_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_field_definitions_tenant_id_aggregate_type_field_name",
                schema: "crm",
                table: "tenant_field_definitions",
                columns: new[] { "tenant_id", "aggregate_type", "field_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence_records",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "opportunity_lines",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "opportunity_needs",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "tenant_field_definitions",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "customer_needs",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "opportunities",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "parties",
                schema: "crm");
        }
    }
}
