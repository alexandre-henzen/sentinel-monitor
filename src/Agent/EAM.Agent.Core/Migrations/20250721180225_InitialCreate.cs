using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EAM.Agent.Core.Migrations
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
                    AgentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Application = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    WindowTitle = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProductivityScore = table.Column<int>(type: "INTEGER", nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    IsSynced = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SessionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ApplicationPath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    WindowTitle = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    SessionType = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductivityScore = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSynced = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrowserEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActivityEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BrowserType = table.Column<int>(type: "INTEGER", nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    PageTitle = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    NavigationType = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    TimeOnPage = table.Column<int>(type: "INTEGER", nullable: false),
                    TabCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActiveTab = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsIncognito = table.Column<bool>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrowserEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrowserEvents_ActivityEvents_ActivityEventId",
                        column: x => x.ActivityEventId,
                        principalTable: "ActivityEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActivityEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ExecutablePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    CommandLine = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    WorkingDirectory = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentProcessId = table.Column<int>(type: "INTEGER", nullable: true),
                    ParentProcessName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    User = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Domain = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MemoryUsage = table.Column<long>(type: "INTEGER", nullable: false),
                    CpuUsage = table.Column<double>(type: "REAL", nullable: false),
                    HandleCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ThreadCount = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExitCode = table.Column<int>(type: "INTEGER", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessEvents_ActivityEvents_ActivityEventId",
                        column: x => x.ActivityEventId,
                        principalTable: "ActivityEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScreenshotEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActivityEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    FileHash = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    Quality = table.Column<int>(type: "INTEGER", nullable: false),
                    Format = table.Column<int>(type: "INTEGER", nullable: false),
                    ForegroundApplication = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ForegroundWindowTitle = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ContainsSensitiveContent = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsProcessed = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsUploaded = table.Column<bool>(type: "INTEGER", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreenshotEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScreenshotEvents_ActivityEvents_ActivityEventId",
                        column: x => x.ActivityEventId,
                        principalTable: "ActivityEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamsEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActivityEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MeetingId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    MeetingTitle = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    IsAudioActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsVideoActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsScreenSharing = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRecording = table.Column<bool>(type: "INTEGER", nullable: false),
                    ParticipantCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamsEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamsEvents_ActivityEvents_ActivityEventId",
                        column: x => x.ActivityEventId,
                        principalTable: "ActivityEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WindowEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActivityEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WindowClassName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ProcessId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ExecutablePath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    X = table.Column<int>(type: "INTEGER", nullable: false),
                    Y = table.Column<int>(type: "INTEGER", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsForeground = table.Column<bool>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WindowEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WindowEvents_ActivityEvents_ActivityEventId",
                        column: x => x.ActivityEventId,
                        principalTable: "ActivityEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_AgentId",
                table: "ActivityEvents",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_AgentId_Timestamp",
                table: "ActivityEvents",
                columns: new[] { "AgentId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_IsSynced",
                table: "ActivityEvents",
                column: "IsSynced");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_Timestamp",
                table: "ActivityEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_Type",
                table: "ActivityEvents",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityEvents_UserId",
                table: "ActivityEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BrowserEvents_ActivityEventId",
                table: "BrowserEvents",
                column: "ActivityEventId");

            migrationBuilder.CreateIndex(
                name: "IX_BrowserEvents_BrowserType",
                table: "BrowserEvents",
                column: "BrowserType");

            migrationBuilder.CreateIndex(
                name: "IX_BrowserEvents_Domain",
                table: "BrowserEvents",
                column: "Domain");

            migrationBuilder.CreateIndex(
                name: "IX_BrowserEvents_Timestamp",
                table: "BrowserEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessEvents_ActivityEventId",
                table: "ProcessEvents",
                column: "ActivityEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessEvents_EventType",
                table: "ProcessEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessEvents_ProcessId",
                table: "ProcessEvents",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessEvents_ProcessName",
                table: "ProcessEvents",
                column: "ProcessName");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessEvents_Timestamp",
                table: "ProcessEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ScreenshotEvents_ActivityEventId",
                table: "ScreenshotEvents",
                column: "ActivityEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreenshotEvents_FileName",
                table: "ScreenshotEvents",
                column: "FileName");

            migrationBuilder.CreateIndex(
                name: "IX_ScreenshotEvents_IsUploaded",
                table: "ScreenshotEvents",
                column: "IsUploaded");

            migrationBuilder.CreateIndex(
                name: "IX_ScreenshotEvents_Timestamp",
                table: "ScreenshotEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_SessionEvents_AgentId",
                table: "SessionEvents",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionEvents_AgentId_StartTime",
                table: "SessionEvents",
                columns: new[] { "AgentId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionEvents_ApplicationName",
                table: "SessionEvents",
                column: "ApplicationName");

            migrationBuilder.CreateIndex(
                name: "IX_SessionEvents_ApplicationName_StartTime",
                table: "SessionEvents",
                columns: new[] { "ApplicationName", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionEvents_IsActive",
                table: "SessionEvents",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SessionEvents_IsSynced",
                table: "SessionEvents",
                column: "IsSynced");

            migrationBuilder.CreateIndex(
                name: "IX_SessionEvents_SessionType",
                table: "SessionEvents",
                column: "SessionType");

            migrationBuilder.CreateIndex(
                name: "IX_SessionEvents_StartTime",
                table: "SessionEvents",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_SessionEvents_UserId",
                table: "SessionEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamsEvents_ActivityEventId",
                table: "TeamsEvents",
                column: "ActivityEventId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamsEvents_EventType",
                table: "TeamsEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_TeamsEvents_MeetingId",
                table: "TeamsEvents",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamsEvents_Timestamp",
                table: "TeamsEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_WindowEvents_ActivityEventId",
                table: "WindowEvents",
                column: "ActivityEventId");

            migrationBuilder.CreateIndex(
                name: "IX_WindowEvents_ProcessId",
                table: "WindowEvents",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_WindowEvents_Timestamp",
                table: "WindowEvents",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrowserEvents");

            migrationBuilder.DropTable(
                name: "ProcessEvents");

            migrationBuilder.DropTable(
                name: "ScreenshotEvents");

            migrationBuilder.DropTable(
                name: "SessionEvents");

            migrationBuilder.DropTable(
                name: "TeamsEvents");

            migrationBuilder.DropTable(
                name: "WindowEvents");

            migrationBuilder.DropTable(
                name: "ActivityEvents");
        }
    }
}
