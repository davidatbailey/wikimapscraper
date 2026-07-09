using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WikiMapScraper.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaceHiddenFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HiddenUtc",
                table: "Places",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Places",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HiddenUtc",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Places");
        }
    }
}
