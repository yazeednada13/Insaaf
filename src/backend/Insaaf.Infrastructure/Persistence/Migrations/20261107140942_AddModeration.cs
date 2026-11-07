using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Insaaf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HiddenAt",
                table: "Reviews",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HiddenByUserId",
                table: "Reviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HiddenReason",
                table: "Reviews",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SuspendedByUserId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_HiddenByUserId",
                table: "Reviews",
                column: "HiddenByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_AspNetUsers_HiddenByUserId",
                table: "Reviews",
                column: "HiddenByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_AspNetUsers_HiddenByUserId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_HiddenByUserId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "HiddenAt",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "HiddenByUserId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "HiddenReason",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SuspendedByUserId",
                table: "AspNetUsers");
        }
    }
}
