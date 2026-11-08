using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Insaaf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PhotoClaimedByUserId",
                table: "VisitTokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PhotoSlotsUsed",
                table: "VisitTokens",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ReviewPhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientUploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VisitTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    SizeBytes = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttachedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PurgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewPhotos", x => x.Id);
                    table.CheckConstraint("CK_ReviewPhotos_AttachedHasOrder", "[ReviewId] IS NULL OR [SortOrder] IS NOT NULL");
                    table.CheckConstraint("CK_ReviewPhotos_ReadyHasFile", "[Status] <> 1 OR [StorageKey] IS NOT NULL");
                    table.CheckConstraint("CK_ReviewPhotos_SortOrder", "[SortOrder] IS NULL OR [SortOrder] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_ReviewPhotos_Status", "[Status] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_ReviewPhotos_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewPhotos_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewPhotos_VisitTokens_VisitTokenId",
                        column: x => x.VisitTokenId,
                        principalTable: "VisitTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_VisitTokens_PhotoSlotsUsed",
                table: "VisitTokens",
                sql: "[PhotoSlotsUsed] BETWEEN 0 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewPhotos_Cleanup",
                table: "ReviewPhotos",
                columns: new[] { "Status", "ReviewId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewPhotos_ReviewId_SortOrder",
                table: "ReviewPhotos",
                columns: new[] { "ReviewId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewPhotos_UploadedByUserId",
                table: "ReviewPhotos",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewPhotos_VisitTokenId_Status",
                table: "ReviewPhotos",
                columns: new[] { "VisitTokenId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewPhotos_VisitTokenId_UploadedByUserId_ClientUploadId",
                table: "ReviewPhotos",
                columns: new[] { "VisitTokenId", "UploadedByUserId", "ClientUploadId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewPhotos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_VisitTokens_PhotoSlotsUsed",
                table: "VisitTokens");

            migrationBuilder.DropColumn(
                name: "PhotoClaimedByUserId",
                table: "VisitTokens");

            migrationBuilder.DropColumn(
                name: "PhotoSlotsUsed",
                table: "VisitTokens");
        }
    }
}
