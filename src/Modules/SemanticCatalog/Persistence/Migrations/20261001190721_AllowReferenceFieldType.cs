using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SemanticCatalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowReferenceFieldType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_field_definitions_field_type",
                schema: "semantic",
                table: "field_definitions");

            migrationBuilder.AddCheckConstraint(
                name: "ck_field_definitions_field_type",
                schema: "semantic",
                table: "field_definitions",
                sql: "field_type IN ('text','long_text','number','decimal','boolean','date','select','multi_select','email','phone','url','reference')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_field_definitions_field_type",
                schema: "semantic",
                table: "field_definitions");

            migrationBuilder.AddCheckConstraint(
                name: "ck_field_definitions_field_type",
                schema: "semantic",
                table: "field_definitions",
                sql: "field_type IN ('text','long_text','number','decimal','boolean','date','select','multi_select','email','phone','url')");
        }
    }
}
