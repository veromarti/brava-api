using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brava.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWishlistItemGifted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "gifted_at",
                table: "wishlist_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_gifted",
                table: "wishlist_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gifted_at",
                table: "wishlist_items");

            migrationBuilder.DropColumn(
                name: "is_gifted",
                table: "wishlist_items");
        }
    }
}
