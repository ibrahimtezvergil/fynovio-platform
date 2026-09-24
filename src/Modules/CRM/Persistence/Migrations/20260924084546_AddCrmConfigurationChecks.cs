using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmConfigurationChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_pipeline_definition_versions_status",
                schema: "crm",
                table: "pipeline_definition_versions",
                sql: "status IN ('Draft', 'Published', 'Superseded', 'Archived')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunity_types_status",
                schema: "crm",
                table: "opportunity_types",
                sql: "status IN ('Active', 'Inactive', 'Archived')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_lost_reasons_status",
                schema: "crm",
                table: "lost_reasons",
                sql: "status IN ('Active', 'Inactive', 'Archived')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_customer_needs_status",
                schema: "crm",
                table: "customer_needs",
                sql: "status IN ('Active', 'Inactive', 'Archived')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_crm_settings_assignment_mode",
                schema: "crm",
                table: "crm_settings",
                sql: "default_assignment_mode IN ('Manual', 'DefaultPrincipal', 'Team', 'Territory')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_crm_settings_assignment_policy",
                schema: "crm",
                table: "crm_settings",
                sql: "assignment_policy IN ('AnyAssignablePrincipal', 'ManagerOnly', 'ManualOnly')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_crm_settings_assignment_reference_required",
                schema: "crm",
                table: "crm_settings",
                sql: "(default_assignment_mode <> 'DefaultPrincipal' OR default_principal_issuer IS NOT NULL) AND (default_assignment_mode <> 'Team' OR default_team_id IS NOT NULL) AND (default_assignment_mode <> 'Territory' OR default_territory_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_crm_settings_creation_mode",
                schema: "crm",
                table: "crm_settings",
                sql: "opportunity_creation_mode IN ('Form', 'Wizard')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_crm_settings_manual_assignment_policy",
                schema: "crm",
                table: "crm_settings",
                sql: "assignment_policy <> 'ManualOnly' OR default_assignment_mode = 'Manual'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_pipeline_definition_versions_status",
                schema: "crm",
                table: "pipeline_definition_versions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunity_types_status",
                schema: "crm",
                table: "opportunity_types");

            migrationBuilder.DropCheckConstraint(
                name: "ck_lost_reasons_status",
                schema: "crm",
                table: "lost_reasons");

            migrationBuilder.DropCheckConstraint(
                name: "ck_customer_needs_status",
                schema: "crm",
                table: "customer_needs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_crm_settings_assignment_mode",
                schema: "crm",
                table: "crm_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_crm_settings_assignment_policy",
                schema: "crm",
                table: "crm_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_crm_settings_assignment_reference_required",
                schema: "crm",
                table: "crm_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_crm_settings_creation_mode",
                schema: "crm",
                table: "crm_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_crm_settings_manual_assignment_policy",
                schema: "crm",
                table: "crm_settings");
        }
    }
}
