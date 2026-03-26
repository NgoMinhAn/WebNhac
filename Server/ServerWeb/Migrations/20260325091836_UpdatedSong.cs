using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServerWeb.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedSong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Album",
                table: "Songs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "Duration",
                table: "Songs",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "Genre",
                table: "Songs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Album",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "Genre",
                table: "Songs");
        }
    }
}
