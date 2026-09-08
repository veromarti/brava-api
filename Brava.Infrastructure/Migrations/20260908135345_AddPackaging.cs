using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brava.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPackaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "packaging_cost",
                table: "orders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "packaging_option_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "packaging_options",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_packaging_options", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_packaging_option_id",
                table: "orders",
                column: "packaging_option_id");

            migrationBuilder.CreateIndex(
                name: "ix_packaging_options_name",
                table: "packaging_options",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_orders_packaging_options_packaging_option_id",
                table: "orders",
                column: "packaging_option_id",
                principalTable: "packaging_options",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_orders_packaging_options_packaging_option_id",
                table: "orders");

            migrationBuilder.DropTable(
                name: "packaging_options");

            migrationBuilder.DropIndex(
                name: "ix_orders_packaging_option_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "packaging_cost",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "packaging_option_id",
                table: "orders");
        }
    }
}
