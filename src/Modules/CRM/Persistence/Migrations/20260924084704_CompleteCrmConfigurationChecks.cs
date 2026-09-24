using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRM.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteCrmConfigurationChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_pipeline_stages_archived_inactive",
                schema: "crm",
                table: "pipeline_stages",
                sql: "NOT is_archived OR NOT is_active");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pipeline_stages_name_non_empty",
                schema: "crm",
                table: "pipeline_stages",
                sql: "length(btrim(name)) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pipeline_stages_sort_order_non_negative",
                schema: "crm",
                table: "pipeline_stages",
                sql: "sort_order >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pipeline_stage_transitions_distinct_stages",
                schema: "crm",
                table: "pipeline_stage_transitions",
                sql: "from_stage_id <> to_stage_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pipeline_definitions_name_non_empty",
                schema: "crm",
                table: "pipeline_definitions",
                sql: "length(btrim(name)) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_pipeline_definition_versions_version_number_positive",
                schema: "crm",
                table: "pipeline_definition_versions",
                sql: "version_number > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_opportunity_types_key_name_non_empty",
                schema: "crm",
                table: "opportunity_types",
                sql: "length(btrim(key)) > 0 AND length(btrim(name)) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_lost_reasons_key_name_non_empty",
                schema: "crm",
                table: "lost_reasons",
                sql: "length(btrim(key)) > 0 AND length(btrim(name)) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_customer_needs_name_non_empty",
                schema: "crm",
                table: "customer_needs",
                sql: "length(btrim(name)) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_pipeline_stages_archived_inactive",
                schema: "crm",
                table: "pipeline_stages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pipeline_stages_name_non_empty",
                schema: "crm",
                table: "pipeline_stages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pipeline_stages_sort_order_non_negative",
                schema: "crm",
                table: "pipeline_stages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pipeline_stage_transitions_distinct_stages",
                schema: "crm",
                table: "pipeline_stage_transitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pipeline_definitions_name_non_empty",
                schema: "crm",
                table: "pipeline_definitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_pipeline_definition_versions_version_number_positive",
                schema: "crm",
                table: "pipeline_definition_versions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_opportunity_types_key_name_non_empty",
                schema: "crm",
                table: "opportunity_types");

            migrationBuilder.DropCheckConstraint(
                name: "ck_lost_reasons_key_name_non_empty",
                schema: "crm",
                table: "lost_reasons");

            migrationBuilder.DropCheckConstraint(
                name: "ck_customer_needs_name_non_empty",
                schema: "crm",
                table: "customer_needs");
        }
    }
}
