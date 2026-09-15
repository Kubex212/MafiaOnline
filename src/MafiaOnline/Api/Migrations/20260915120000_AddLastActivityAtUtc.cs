using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLastActivityAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the column with a server-side default so existing rows are not immediately
            // treated as inactive by the cleanup service.
            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAtUtc",
                table: "Lobbies",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.CreateIndex(
                name: "IX_Lobbies_LastActivityAtUtc",
                table: "Lobbies",
                column: "LastActivityAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lobbies_LastActivityAtUtc",
                table: "Lobbies");

            migrationBuilder.DropColumn(
                name: "LastActivityAtUtc",
                table: "Lobbies");
        }
    }
}
