using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureOpportunityCreationSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "opportunity_creation_steps",
                schema: "crm",
                table: "crm_settings",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[\"Customer\",\"Needs\",\"Products\"]'::jsonb");

            migrationBuilder.AddCheckConstraint(
                name: "ck_crm_settings_creation_steps",
                schema: "crm",
                table: "crm_settings",
                sql: "jsonb_typeof(opportunity_creation_steps) = 'array' AND jsonb_array_length(opportunity_creation_steps) BETWEEN 1 AND 3 AND opportunity_creation_steps @> '[\"Customer\"]'::jsonb AND opportunity_creation_steps <@ '[\"Customer\",\"Needs\",\"Products\"]'::jsonb AND (opportunity_creation_steps ->> 0) IS DISTINCT FROM (opportunity_creation_steps ->> 1) AND (opportunity_creation_steps ->> 0) IS DISTINCT FROM (opportunity_creation_steps ->> 2) AND (opportunity_creation_steps ->> 1) IS DISTINCT FROM (opportunity_creation_steps ->> 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_crm_settings_creation_steps",
                schema: "crm",
                table: "crm_settings");

            migrationBuilder.DropColumn(
                name: "opportunity_creation_steps",
                schema: "crm",
                table: "crm_settings");
        }
    }
}
