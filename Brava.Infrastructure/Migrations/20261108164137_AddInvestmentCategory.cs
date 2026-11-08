using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brava.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "category",
                table: "investments",
                type: "integer",
                nullable: false,
                defaultValue: 4);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "category",
                table: "investments");
        }
    }
}
