using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlackjackBot.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPremiumFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Diamonds",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "HasActiveBooster",
                table: "Players",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasActiveMegaBooster",
                table: "Players",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsLastLossRewinded",
                table: "Players",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastLossTime",
                table: "Players",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<long>(
                name: "LastLostBet",
                table: "Players",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VipUntil",
                table: "Players",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Diamonds",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "HasActiveBooster",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "HasActiveMegaBooster",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "IsLastLossRewinded",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastLossTime",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastLostBet",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "VipUntil",
                table: "Players");
        }
    }
}
