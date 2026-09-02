using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brava.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddComboImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "image_storage_key",
                table: "combos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "image_storage_key",
                table: "combos");
        }
    }
}
