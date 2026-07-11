using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlackjackBot.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMinesweeperMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinesGamesPlayed",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinesLosses",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "MinesTotalMoneyLost",
                table: "Players",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MinesTotalMoneyWon",
                table: "Players",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "MinesWins",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinesGamesPlayed",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "MinesLosses",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "MinesTotalMoneyLost",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "MinesTotalMoneyWon",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "MinesWins",
                table: "Players");
        }
    }
}
