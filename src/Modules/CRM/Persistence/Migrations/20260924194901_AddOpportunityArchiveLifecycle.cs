using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityArchiveLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "archived_at",
                schema: "crm",
                table: "opportunities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                schema: "crm",
                table: "opportunities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_opportunities_tenant_id_is_archived_created_at",
                schema: "crm",
                table: "opportunities",
                columns: new[] { "tenant_id", "is_archived", "created_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunities_archive_nonterminal",
                schema: "crm",
                table: "opportunities",
                sql: "NOT is_archived OR status IN ('draft','open')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunities_archive_timestamp",
                schema: "crm",
                table: "opportunities",
                sql: "(is_archived AND archived_at IS NOT NULL) OR (NOT is_archived AND archived_at IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_opportunities_tenant_id_is_archived_created_at",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunities_archive_nonterminal",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunities_archive_timestamp",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "archived_at",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropColumn(
                name: "is_archived",
                schema: "crm",
                table: "opportunities");
        }
    }
}
