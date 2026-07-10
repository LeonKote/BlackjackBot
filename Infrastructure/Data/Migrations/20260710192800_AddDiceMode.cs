using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlackjackBot.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiceMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiceGamesPlayed",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DiceLosses",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "DiceTotalMoneyLost",
                table: "Players",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "DiceTotalMoneyWon",
                table: "Players",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "DiceWins",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiceGamesPlayed",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DiceLosses",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DiceTotalMoneyLost",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DiceTotalMoneyWon",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DiceWins",
                table: "Players");
        }
    }
}
