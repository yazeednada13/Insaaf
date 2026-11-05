using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Insaaf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewComments", x => x.Id);
                    table.CheckConstraint("CK_ReviewComments_TextNotBlank", "LEN([Text]) > 0");
                    table.ForeignKey(
                        name: "FK_ReviewComments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewComments_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewLikes",
                columns: table => new
                {
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewLikes", x => new { x.ReviewId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ReviewLikes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewLikes_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewComments_ReviewId_CreatedAt",
                table: "ReviewComments",
                columns: new[] { "ReviewId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewComments_UserId",
                table: "ReviewComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLikes_UserId",
                table: "ReviewLikes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewComments");

            migrationBuilder.DropTable(
                name: "ReviewLikes");
        }
    }
}
