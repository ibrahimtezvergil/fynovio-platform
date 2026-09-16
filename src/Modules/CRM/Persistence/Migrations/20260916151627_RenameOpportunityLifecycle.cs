using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameOpportunityLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunities_cancel_fields_required_once_canceled",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunities_expiry_required_once_offered",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunities_sale_date_required_once_completed",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunities_status",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.RenameColumn(
                name: "sale_date",
                schema: "crm",
                table: "opportunities",
                newName: "won_date");

            migrationBuilder.RenameColumn(
                name: "offer_date",
                schema: "crm",
                table: "opportunities",
                newName: "opened_date");

            migrationBuilder.RenameColumn(
                name: "cancel_reason",
                schema: "crm",
                table: "opportunities",
                newName: "lost_reason");

            migrationBuilder.RenameColumn(
                name: "cancel_date",
                schema: "crm",
                table: "opportunities",
                newName: "lost_date");

            migrationBuilder.Sql("""
                UPDATE crm.opportunities SET status = CASE status
                    WHEN 'waiting' THEN 'draft'
                    WHEN 'offered' THEN 'open'
                    WHEN 'completed' THEN 'won'
                    WHEN 'canceled' THEN 'lost'
                    ELSE status
                END;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunities_expiry_required_once_open",
                schema: "crm",
                table: "opportunities",
                sql: "status NOT IN ('open','won') OR expiry_date IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunities_lost_fields_required_once_lost",
                schema: "crm",
                table: "opportunities",
                sql: "status <> 'lost' OR (lost_date IS NOT NULL AND lost_reason IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunities_status",
                schema: "crm",
                table: "opportunities",
                sql: "status IN ('draft','open','won','lost')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunities_won_date_required_once_won",
                schema: "crm",
                table: "opportunities",
                sql: "status <> 'won' OR won_date IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunities_expiry_required_once_open",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunities_lost_fields_required_once_lost",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunities_status",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunities_won_date_required_once_won",
                schema: "crm",
                table: "opportunities");

            migrationBuilder.RenameColumn(
                name: "won_date",
                schema: "crm",
                table: "opportunities",
                newName: "sale_date");

            migrationBuilder.RenameColumn(
                name: "opened_date",
                schema: "crm",
                table: "opportunities",
                newName: "offer_date");

            migrationBuilder.RenameColumn(
                name: "lost_reason",
                schema: "crm",
                table: "opportunities",
                newName: "cancel_reason");

            migrationBuilder.RenameColumn(
                name: "lost_date",
                schema: "crm",
                table: "opportunities",
                newName: "cancel_date");

            migrationBuilder.Sql("""
                UPDATE crm.opportunities SET status = CASE status
                    WHEN 'draft' THEN 'waiting'
                    WHEN 'open' THEN 'offered'
                    WHEN 'won' THEN 'completed'
                    WHEN 'lost' THEN 'canceled'
                    ELSE status
                END;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunities_cancel_fields_required_once_canceled",
                schema: "crm",
                table: "opportunities",
                sql: "status <> 'canceled' OR (cancel_date IS NOT NULL AND cancel_reason IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunities_expiry_required_once_offered",
                schema: "crm",
                table: "opportunities",
                sql: "status NOT IN ('offered','completed') OR expiry_date IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunities_sale_date_required_once_completed",
                schema: "crm",
                table: "opportunities",
                sql: "status <> 'completed' OR sale_date IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunities_status",
                schema: "crm",
                table: "opportunities",
                sql: "status IN ('waiting','offered','completed','canceled')");
        }
    }
}
