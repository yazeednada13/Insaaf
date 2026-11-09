using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Insaaf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletionFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarKey",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyComments",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyFollows",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyLikes",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "TastePriorities",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TasteSetupCompletedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReviewCommentLikes",
                columns: table => new
                {
                    CommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewCommentLikes", x => new { x.CommentId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ReviewCommentLikes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewCommentLikes_ReviewComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "ReviewComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewReplies",
                columns: table => new
                {
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    WrittenByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewReplies", x => x.ReviewId);
                    table.ForeignKey(
                        name: "FK_ReviewReplies_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewReports_AspNetUsers_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewReports_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedRestaurants",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedRestaurants", x => new { x.UserId, x.RestaurantId });
                    table.ForeignKey(
                        name: "FK_SavedRestaurants_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavedRestaurants_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavedReviews",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedReviews", x => new { x.UserId, x.ReviewId });
                    table.ForeignKey(
                        name: "FK_SavedReviews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavedReviews_Reviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFoodPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFoodPreferences", x => new { x.UserId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_UserFoodPreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserFoodPreferences_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCommentLikes_UserId",
                table: "ReviewCommentLikes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewReports_Open_PerReporter",
                table: "ReviewReports",
                columns: new[] { "ReviewId", "ReporterUserId" },
                unique: true,
                filter: "[ResolvedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewReports_ReporterUserId",
                table: "ReviewReports",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewReports_ResolvedAt_CreatedAt",
                table: "ReviewReports",
                columns: new[] { "ResolvedAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedRestaurants_RestaurantId",
                table: "SavedRestaurants",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedRestaurants_UserId_CreatedAt",
                table: "SavedRestaurants",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SavedReviews_ReviewId",
                table: "SavedReviews",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedReviews_UserId_CreatedAt",
                table: "SavedReviews",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_UserFoodPreferences_CategoryId",
                table: "UserFoodPreferences",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewCommentLikes");

            migrationBuilder.DropTable(
                name: "ReviewReplies");

            migrationBuilder.DropTable(
                name: "ReviewReports");

            migrationBuilder.DropTable(
                name: "SavedRestaurants");

            migrationBuilder.DropTable(
                name: "SavedReviews");

            migrationBuilder.DropTable(
                name: "UserFoodPreferences");

            migrationBuilder.DropColumn(
                name: "AvatarKey",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotifyComments",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotifyFollows",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotifyLikes",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TastePriorities",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TasteSetupCompletedAt",
                table: "AspNetUsers");
        }
    }
}
