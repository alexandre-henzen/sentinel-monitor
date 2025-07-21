using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EAM.Agent.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddDurationSecondsToSessionEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "SessionEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "SessionEvents");
        }
    }
}
