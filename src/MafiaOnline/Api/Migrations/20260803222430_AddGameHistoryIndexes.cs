using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGameHistoryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameHistory_PlayedAtUtc",
                table: "GameHistory");

            migrationBuilder.CreateIndex(
                name: "IX_GameHistory_UserId_PlayedAtUtc",
                table: "GameHistory",
                columns: new[] { "UserId", "PlayedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GameHistory_UserId_PlayedAtUtc",
                table: "GameHistory");

            migrationBuilder.CreateIndex(
                name: "IX_GameHistory_PlayedAtUtc",
                table: "GameHistory",
                column: "PlayedAtUtc");
        }
    }
}
