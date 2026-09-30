using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandTenantFieldDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_field_definitions_field_type",
                schema: "crm",
                table: "tenant_field_definitions");

            migrationBuilder.AlterColumn<string>(
                name: "field_name",
                schema: "crm",
                table: "tenant_field_definitions",
                type: "character varying(63)",
                maxLength: 63,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "config",
                schema: "crm",
                table: "tenant_field_definitions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "label",
                schema: "crm",
                table: "tenant_field_definitions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "owner_scope",
                schema: "crm",
                table: "tenant_field_definitions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Tenant");

            migrationBuilder.AddColumn<long>(
                name: "row_version",
                schema: "crm",
                table: "tenant_field_definitions",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                schema: "crm",
                table: "tenant_field_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "crm",
                table: "tenant_field_definitions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                schema: "crm",
                table: "tenant_field_definitions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_field_definitions_config_object",
                schema: "crm",
                table: "tenant_field_definitions",
                sql: "jsonb_typeof(config) = 'object'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_field_definitions_field_name",
                schema: "crm",
                table: "tenant_field_definitions",
                sql: "field_name ~ '^[a-z][a-z0-9_]{1,62}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_field_definitions_field_type",
                schema: "crm",
                table: "tenant_field_definitions",
                sql: "field_type IN ('text','long_text','number','decimal','boolean','date','select','multi_select','email','phone','url')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_field_definitions_owner_scope",
                schema: "crm",
                table: "tenant_field_definitions",
                sql: "owner_scope = 'Tenant'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_field_definitions_sort_order",
                schema: "crm",
                table: "tenant_field_definitions",
                sql: "sort_order BETWEEN 0 AND 10000");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_field_definitions_status",
                schema: "crm",
                table: "tenant_field_definitions",
                sql: "status IN ('Active','Deprecated')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_field_definitions_config_object",
                schema: "crm",
                table: "tenant_field_definitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_field_definitions_field_name",
                schema: "crm",
                table: "tenant_field_definitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_field_definitions_field_type",
                schema: "crm",
                table: "tenant_field_definitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_field_definitions_owner_scope",
                schema: "crm",
                table: "tenant_field_definitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_field_definitions_sort_order",
                schema: "crm",
                table: "tenant_field_definitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_field_definitions_status",
                schema: "crm",
                table: "tenant_field_definitions");

            migrationBuilder.DropColumn(
                name: "config",
                schema: "crm",
                table: "tenant_field_definitions");

            migrationBuilder.DropColumn(
                name: "label",
                schema: "crm",
                table: "tenant_field_definitions");

            migrationBuilder.DropColumn(
                name: "owner_scope",
                schema: "crm",
                table: "tenant_field_definitions");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "crm",
                table: "tenant_field_definitions");

            migrationBuilder.DropColumn(
                name: "sort_order",
                schema: "crm",
                table: "tenant_field_definitions");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "crm",
                table: "tenant_field_definitions");

            migrationBuilder.DropColumn(
                name: "updated_at",
                schema: "crm",
                table: "tenant_field_definitions");

            migrationBuilder.AlterColumn<string>(
                name: "field_name",
                schema: "crm",
                table: "tenant_field_definitions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(63)",
                oldMaxLength: 63);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_field_definitions_field_type",
                schema: "crm",
                table: "tenant_field_definitions",
                sql: "field_type IN ('text','number','boolean','date')");
        }
    }
}
