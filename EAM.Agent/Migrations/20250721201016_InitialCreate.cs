using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EAM.Agent.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    MachineName = table.Column<string>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", nullable: true),
                    WindowTitle = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityEvents", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityEvents");
        }
    }
}
