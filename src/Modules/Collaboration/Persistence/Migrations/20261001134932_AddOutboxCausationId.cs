using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collaboration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxCausationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "causation_id",
                schema: "collaboration",
                table: "outbox_messages",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "causation_id",
                schema: "collaboration",
                table: "outbox_messages");
        }
    }
}
