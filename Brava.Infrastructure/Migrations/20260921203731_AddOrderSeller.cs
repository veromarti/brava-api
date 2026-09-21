using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brava.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderSeller : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "seller",
                table: "orders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "seller",
                table: "orders");
        }
    }
}
