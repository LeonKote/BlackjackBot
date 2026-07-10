using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlackjackBot.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeparateStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Wins",
                table: "Players",
                newName: "CrashWins");

            migrationBuilder.RenameColumn(
                name: "TotalMoneyWon",
                table: "Players",
                newName: "CrashTotalMoneyWon");

            migrationBuilder.RenameColumn(
                name: "TotalMoneyLost",
                table: "Players",
                newName: "CrashTotalMoneyLost");

            migrationBuilder.RenameColumn(
                name: "Losses",
                table: "Players",
                newName: "CrashLosses");

            migrationBuilder.RenameColumn(
                name: "GamesPlayed",
                table: "Players",
                newName: "CrashGamesPlayed");

            migrationBuilder.RenameColumn(
                name: "Draws",
                table: "Players",
                newName: "BjWins");

            migrationBuilder.AddColumn<int>(
                name: "BjDraws",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BjGamesPlayed",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BjLosses",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "BjTotalMoneyLost",
                table: "Players",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "BjTotalMoneyWon",
                table: "Players",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BjDraws",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "BjGamesPlayed",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "BjLosses",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "BjTotalMoneyLost",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "BjTotalMoneyWon",
                table: "Players");

            migrationBuilder.RenameColumn(
                name: "CrashWins",
                table: "Players",
                newName: "Wins");

            migrationBuilder.RenameColumn(
                name: "CrashTotalMoneyWon",
                table: "Players",
                newName: "TotalMoneyWon");

            migrationBuilder.RenameColumn(
                name: "CrashTotalMoneyLost",
                table: "Players",
                newName: "TotalMoneyLost");

            migrationBuilder.RenameColumn(
                name: "CrashLosses",
                table: "Players",
                newName: "Losses");

            migrationBuilder.RenameColumn(
                name: "CrashGamesPlayed",
                table: "Players",
                newName: "GamesPlayed");

            migrationBuilder.RenameColumn(
                name: "BjWins",
                table: "Players",
                newName: "Draws");
        }
    }
}
