using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeSnifferDog.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StoredZipRelativePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    QueueTimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessingStartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectAgentGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuntimeKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAgentGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAgentGroups_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectRuleReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RuleKeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MarkdownContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectRuleReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectRuleReports_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectAgents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectAgentGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuntimeKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAgents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAgents_ProjectAgentGroups_ProjectAgentGroupId",
                        column: x => x.ProjectAgentGroupId,
                        principalTable: "ProjectAgentGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectAgentTimelineEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectAgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    EntryType = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ToolName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ToolCallId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ToolArguments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ToolResult = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAgentTimelineEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAgentTimelineEntries_ProjectAgents_ProjectAgentId",
                        column: x => x.ProjectAgentId,
                        principalTable: "ProjectAgents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentGroups_ProjectId_CreatedAtUtc",
                table: "ProjectAgentGroups",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentGroups_ProjectId_RuntimeKey",
                table: "ProjectAgentGroups",
                columns: new[] { "ProjectId", "RuntimeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgents_ProjectAgentGroupId_CreatedAtUtc",
                table: "ProjectAgents",
                columns: new[] { "ProjectAgentGroupId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgents_ProjectAgentGroupId_RuntimeKey",
                table: "ProjectAgents",
                columns: new[] { "ProjectAgentGroupId", "RuntimeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentTimelineEntries_ProjectAgentId_OccurredAtUtc",
                table: "ProjectAgentTimelineEntries",
                columns: new[] { "ProjectAgentId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentTimelineEntries_ProjectAgentId_Sequence",
                table: "ProjectAgentTimelineEntries",
                columns: new[] { "ProjectAgentId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentTimelineEntries_ProjectAgentId_ToolCallId",
                table: "ProjectAgentTimelineEntries",
                columns: new[] { "ProjectAgentId", "ToolCallId" },
                unique: true,
                filter: "[ToolCallId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectRuleReports_ProjectId_RuleKeyHash",
                table: "ProjectRuleReports",
                columns: new[] { "ProjectId", "RuleKeyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status_QueueTimestampUtc_CreatedAtUtc",
                table: "Projects",
                columns: new[] { "Status", "QueueTimestampUtc", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectAgentTimelineEntries");

            migrationBuilder.DropTable(
                name: "ProjectRuleReports");

            migrationBuilder.DropTable(
                name: "ProjectAgents");

            migrationBuilder.DropTable(
                name: "ProjectAgentGroups");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
