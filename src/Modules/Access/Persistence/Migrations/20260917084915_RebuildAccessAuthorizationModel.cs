using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Access.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RebuildAccessAuthorizationModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_role_assignments_roles_role_id",
                schema: "access",
                table: "role_assignments");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "access");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "access");

            migrationBuilder.DropIndex(
                name: "ix_role_assignments_role_id",
                schema: "access",
                table: "role_assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_role_assignments_scope_type",
                schema: "access",
                table: "role_assignments");

            migrationBuilder.DropColumn(
                name: "is_system",
                schema: "access",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "scope_id",
                schema: "access",
                table: "role_assignments");

            migrationBuilder.RenameColumn(
                name: "scope_type",
                schema: "access",
                table: "role_assignments",
                newName: "source");

            migrationBuilder.AlterColumn<long>(
                name: "tenant_id",
                schema: "access",
                table: "roles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "key",
                schema: "access",
                table: "roles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "origin",
                schema: "access",
                table: "roles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "granted_by_account_id",
                schema: "access",
                table: "role_assignments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "principal_type",
                schema: "access",
                table: "role_assignments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "reason",
                schema: "access",
                table: "role_assignments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_roles_tenant_id_id",
                schema: "access",
                table: "roles",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateTable(
                name: "actions",
                schema: "access",
                columns: table => new
                {
                    action_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    owner_module = table.Column<string>(type: "text", nullable: false),
                    resource_type = table.Column<string>(type: "text", nullable: false),
                    risk_class = table.Column<string>(type: "text", nullable: true),
                    is_deprecated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_actions", x => x.action_key);
                });

            migrationBuilder.CreateTable(
                name: "evidence_records",
                schema: "access",
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
                schema: "access",
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
                schema: "access",
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
                name: "permission_sets",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission_sets", x => x.id);
                    table.UniqueConstraint("ak_permission_sets_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_permission_sets_origin", "origin IN ('tenant','system_template')");
                });

            migrationBuilder.CreateTable(
                name: "tenant_access_state",
                schema: "access",
                columns: table => new
                {
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_access_state", x => x.tenant_id);
                });

            migrationBuilder.CreateTable(
                name: "permission_set_items",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    permission_set_id = table.Column<long>(type: "bigint", nullable: false),
                    action_key = table.Column<string>(type: "character varying(200)", nullable: false),
                    relation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permission_set_items", x => x.id);
                    table.CheckConstraint("ck_permission_set_items_relation", "relation IS NULL OR relation = 'owner'");
                    table.ForeignKey(
                        name: "fk_permission_set_items_actions_action_key",
                        column: x => x.action_key,
                        principalSchema: "access",
                        principalTable: "actions",
                        principalColumn: "action_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_permission_set_items_permission_sets_tenant_id_permission_s",
                        columns: x => new { x.tenant_id, x.permission_set_id },
                        principalSchema: "access",
                        principalTable: "permission_sets",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permission_sets",
                schema: "access",
                columns: table => new
                {
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    permission_set_id = table.Column<long>(type: "bigint", nullable: false),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permission_sets", x => new { x.role_id, x.permission_set_id });
                    table.ForeignKey(
                        name: "fk_role_permission_sets_permission_sets_tenant_id_permission_s",
                        columns: x => new { x.tenant_id, x.permission_set_id },
                        principalSchema: "access",
                        principalTable: "permission_sets",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_permission_sets_roles_tenant_id_role_id",
                        columns: x => new { x.tenant_id, x.role_id },
                        principalSchema: "access",
                        principalTable: "roles",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_roles_tenant_id_id",
                schema: "access",
                table: "roles",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roles_tenant_id_key",
                schema: "access",
                table: "roles",
                columns: new[] { "tenant_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roles_tenant_id_name",
                schema: "access",
                table: "roles",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_roles_origin",
                schema: "access",
                table: "roles",
                sql: "origin IN ('tenant','system_template')");

            migrationBuilder.CreateIndex(
                name: "ix_role_assignments_tenant_id_role_id",
                schema: "access",
                table: "role_assignments",
                columns: new[] { "tenant_id", "role_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_role_assignments_principal_type",
                schema: "access",
                table: "role_assignments",
                sql: "principal_type = 'user'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_role_assignments_source",
                schema: "access",
                table: "role_assignments",
                sql: "source IN ('manual','bootstrap')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_role_assignments_valid_range",
                schema: "access",
                table: "role_assignments",
                sql: "valid_to IS NULL OR valid_to > valid_from");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_records_correlation_id",
                schema: "access",
                table: "evidence_records",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_records_tenant_id_aggregate_type_aggregate_id",
                schema: "access",
                table: "evidence_records",
                columns: new[] { "tenant_id", "aggregate_type", "aggregate_id" });

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_records_expires_at",
                schema: "access",
                table: "idempotency_records",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_id",
                schema: "access",
                table: "outbox_messages",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_tenant_id_processed_at",
                schema: "access",
                table: "outbox_messages",
                columns: new[] { "tenant_id", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_permission_set_items_action_key",
                schema: "access",
                table: "permission_set_items",
                column: "action_key");

            migrationBuilder.CreateIndex(
                name: "ix_permission_set_items_tenant_id_action_key",
                schema: "access",
                table: "permission_set_items",
                columns: new[] { "tenant_id", "action_key" });

            migrationBuilder.CreateIndex(
                name: "ix_permission_set_items_tenant_id_permission_set_id",
                schema: "access",
                table: "permission_set_items",
                columns: new[] { "tenant_id", "permission_set_id" });

            migrationBuilder.CreateIndex(
                name: "ix_permission_sets_tenant_id_id",
                schema: "access",
                table: "permission_sets",
                columns: new[] { "tenant_id", "id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_permission_sets_tenant_id_key",
                schema: "access",
                table: "permission_sets",
                columns: new[] { "tenant_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_permission_sets_tenant_id_permission_set_id",
                schema: "access",
                table: "role_permission_sets",
                columns: new[] { "tenant_id", "permission_set_id" });

            migrationBuilder.CreateIndex(
                name: "ix_role_permission_sets_tenant_id_role_id",
                schema: "access",
                table: "role_permission_sets",
                columns: new[] { "tenant_id", "role_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_role_assignments_roles_tenant_id_role_id",
                schema: "access",
                table: "role_assignments",
                columns: new[] { "tenant_id", "role_id" },
                principalSchema: "access",
                principalTable: "roles",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_role_assignments_roles_tenant_id_role_id",
                schema: "access",
                table: "role_assignments");

            migrationBuilder.DropTable(
                name: "evidence_records",
                schema: "access");

            migrationBuilder.DropTable(
                name: "idempotency_records",
                schema: "access");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "access");

            migrationBuilder.DropTable(
                name: "permission_set_items",
                schema: "access");

            migrationBuilder.DropTable(
                name: "role_permission_sets",
                schema: "access");

            migrationBuilder.DropTable(
                name: "tenant_access_state",
                schema: "access");

            migrationBuilder.DropTable(
                name: "actions",
                schema: "access");

            migrationBuilder.DropTable(
                name: "permission_sets",
                schema: "access");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_roles_tenant_id_id",
                schema: "access",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_roles_tenant_id_id",
                schema: "access",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_roles_tenant_id_key",
                schema: "access",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_roles_tenant_id_name",
                schema: "access",
                table: "roles");

            migrationBuilder.DropCheckConstraint(
                name: "ck_roles_origin",
                schema: "access",
                table: "roles");

            migrationBuilder.DropIndex(
                name: "ix_role_assignments_tenant_id_role_id",
                schema: "access",
                table: "role_assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_role_assignments_principal_type",
                schema: "access",
                table: "role_assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_role_assignments_source",
                schema: "access",
                table: "role_assignments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_role_assignments_valid_range",
                schema: "access",
                table: "role_assignments");

            migrationBuilder.DropColumn(
                name: "key",
                schema: "access",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "origin",
                schema: "access",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "granted_by_account_id",
                schema: "access",
                table: "role_assignments");

            migrationBuilder.DropColumn(
                name: "principal_type",
                schema: "access",
                table: "role_assignments");

            migrationBuilder.DropColumn(
                name: "reason",
                schema: "access",
                table: "role_assignments");

            migrationBuilder.RenameColumn(
                name: "source",
                schema: "access",
                table: "role_assignments",
                newName: "scope_type");

            migrationBuilder.AlterColumn<long>(
                name: "tenant_id",
                schema: "access",
                table: "roles",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<bool>(
                name: "is_system",
                schema: "access",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "scope_id",
                schema: "access",
                table: "role_assignments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    description = table.Column<string>(type: "text", nullable: true),
                    key = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "access",
                columns: table => new
                {
                    role_id = table.Column<long>(type: "bigint", nullable: false),
                    permission_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "fk_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "access",
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "access",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_role_assignments_role_id",
                schema: "access",
                table: "role_assignments",
                column: "role_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_role_assignments_scope_type",
                schema: "access",
                table: "role_assignments",
                sql: "scope_type IN ('tenant','organization_unit','network')");

            migrationBuilder.CreateIndex(
                name: "ix_permissions_key",
                schema: "access",
                table: "permissions",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_permission_id",
                schema: "access",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.AddForeignKey(
                name: "fk_role_assignments_roles_role_id",
                schema: "access",
                table: "role_assignments",
                column: "role_id",
                principalSchema: "access",
                principalTable: "roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
