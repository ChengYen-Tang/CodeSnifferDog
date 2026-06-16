using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeSnifferDog.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectAgentSystemPrompt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SystemPrompt",
                table: "ProjectAgents",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SystemPrompt",
                table: "ProjectAgents");
        }
    }
}
