using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Identity;
using Insaaf.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Insaaf.Infrastructure.Persistence;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Restaurant> Restaurants => Set<Restaurant>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<RestaurantCategory> RestaurantCategories => Set<RestaurantCategory>();

    public DbSet<RestaurantOpeningHours> RestaurantOpeningHours => Set<RestaurantOpeningHours>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<VisitToken> VisitTokens => Set<VisitToken>();

    public DbSet<ReviewLike> ReviewLikes => Set<ReviewLike>();

    public DbSet<ReviewComment> ReviewComments => Set<ReviewComment>();

    public DbSet<UserFollow> UserFollows => Set<UserFollow>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<ReviewPhoto> ReviewPhotos => Set<ReviewPhoto>();

    // Phase 11.
    public DbSet<SavedRestaurant> SavedRestaurants => Set<SavedRestaurant>();

    public DbSet<SavedReview> SavedReviews => Set<SavedReview>();

    public DbSet<ReviewCommentLike> ReviewCommentLikes => Set<ReviewCommentLike>();

    public DbSet<ReviewReport> ReviewReports => Set<ReviewReport>();

    public DbSet<ReviewReply> ReviewReplies => Set<ReviewReply>();

    public DbSet<UserFoodPreference> UserFoodPreferences => Set<UserFoodPreference>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new ApplicationUserConfiguration());
        builder.ApplyConfiguration(new RefreshTokenConfiguration());
        builder.ApplyConfiguration(new RestaurantConfiguration());
        builder.ApplyConfiguration(new CategoryConfiguration());
        builder.ApplyConfiguration(new RestaurantCategoryConfiguration());
        builder.ApplyConfiguration(new RestaurantOpeningHoursConfiguration());
        builder.ApplyConfiguration(new ReviewConfiguration());
        builder.ApplyConfiguration(new VisitTokenConfiguration());
        builder.ApplyConfiguration(new ReviewLikeConfiguration());
        builder.ApplyConfiguration(new ReviewCommentConfiguration());
        builder.ApplyConfiguration(new UserFollowConfiguration());
        builder.ApplyConfiguration(new NotificationConfiguration());
        builder.ApplyConfiguration(new ReviewPhotoConfiguration());
        builder.ApplyConfiguration(new SavedRestaurantConfiguration());
        builder.ApplyConfiguration(new SavedReviewConfiguration());
        builder.ApplyConfiguration(new ReviewCommentLikeConfiguration());
        builder.ApplyConfiguration(new ReviewReportConfiguration());
        builder.ApplyConfiguration(new ReviewReplyConfiguration());
        builder.ApplyConfiguration(new UserFoodPreferenceConfiguration());
    }
}
