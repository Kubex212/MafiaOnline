using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLobbyGameHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LobbyCode",
                table: "GameHistory",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "LobbyId",
                table: "GameHistory",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "LobbyGames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LobbyId = table.Column<Guid>(type: "uuid", nullable: false),
                    LobbyCode = table.Column<string>(type: "text", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Winner = table.Column<string>(type: "text", nullable: false),
                    PlayerCount = table.Column<int>(type: "integer", nullable: false),
                    DayCount = table.Column<int>(type: "integer", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    ConfigJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LobbyGames", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameHistory_LobbyId",
                table: "GameHistory",
                column: "LobbyId");

            migrationBuilder.CreateIndex(
                name: "IX_GameHistory_LobbyId_PlayedAtUtc",
                table: "GameHistory",
                columns: new[] { "LobbyId", "PlayedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LobbyGames_LobbyCode",
                table: "LobbyGames",
                column: "LobbyCode");

            migrationBuilder.CreateIndex(
                name: "IX_LobbyGames_LobbyId",
                table: "LobbyGames",
                column: "LobbyId");

            migrationBuilder.CreateIndex(
                name: "IX_LobbyGames_LobbyId_FinishedAtUtc",
                table: "LobbyGames",
                columns: new[] { "LobbyId", "FinishedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LobbyGames");

            migrationBuilder.DropIndex(
                name: "IX_GameHistory_LobbyId",
                table: "GameHistory");

            migrationBuilder.DropIndex(
                name: "IX_GameHistory_LobbyId_PlayedAtUtc",
                table: "GameHistory");

            migrationBuilder.DropColumn(
                name: "LobbyCode",
                table: "GameHistory");

            migrationBuilder.DropColumn(
                name: "LobbyId",
                table: "GameHistory");
        }
    }
}
