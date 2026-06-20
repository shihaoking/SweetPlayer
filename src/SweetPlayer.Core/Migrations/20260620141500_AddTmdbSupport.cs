using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SweetPlayer.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddTmdbSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TmdbId",
                table: "MovieMetadata",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DataSource",
                table: "MovieMetadata",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TmdbId",
                table: "MovieMetadata");

            migrationBuilder.DropColumn(
                name: "DataSource",
                table: "MovieMetadata");
        }
    }
}
