using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropertyManagementPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenancePriorityAndNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignmentNotes",
                table: "MaintenanceRequests",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "MaintenanceRequests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignmentNotes",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "MaintenanceRequests");
        }
    }
}
