using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Insaaf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPartner",
                table: "Restaurants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PartnerSince",
                table: "Restaurants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VisitTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuanceChannel = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RedeemedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RedeemedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitTokens", x => x.Id);
                    table.CheckConstraint("CK_VisitTokens_ExpiresAfterIssued", "[ExpiresAt] > [IssuedAt]");
                    table.CheckConstraint("CK_VisitTokens_ManualRequiresIssuer", "[IssuanceChannel] <> 1 OR [IssuedByUserId] IS NOT NULL");
                    table.CheckConstraint("CK_VisitTokens_RedeemerRequiresRedemption", "[RedeemedByUserId] IS NULL OR [RedeemedAt] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_VisitTokens_AspNetUsers_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitTokens_AspNetUsers_RedeemedByUserId",
                        column: x => x.RedeemedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitTokens_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_VisitTokenId",
                table: "Reviews",
                column: "VisitTokenId",
                unique: true,
                filter: "[VisitTokenId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VisitTokens_IssuanceId",
                table: "VisitTokens",
                column: "IssuanceId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitTokens_IssuedByUserId",
                table: "VisitTokens",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitTokens_RedeemedByUserId",
                table: "VisitTokens",
                column: "RedeemedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitTokens_RestaurantId_RedeemedAt_RevokedAt",
                table: "VisitTokens",
                columns: new[] { "RestaurantId", "RedeemedAt", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VisitTokens_TokenHash",
                table: "VisitTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_VisitTokens_VisitTokenId",
                table: "Reviews",
                column: "VisitTokenId",
                principalTable: "VisitTokens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_VisitTokens_VisitTokenId",
                table: "Reviews");

            migrationBuilder.DropTable(
                name: "VisitTokens");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_VisitTokenId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "IsPartner",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "PartnerSince",
                table: "Restaurants");
        }
    }
}
