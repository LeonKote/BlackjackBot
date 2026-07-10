using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlackjackBot.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrashMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GameType",
                table: "GameHistories",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GameType",
                table: "GameHistories");
        }
    }
}
