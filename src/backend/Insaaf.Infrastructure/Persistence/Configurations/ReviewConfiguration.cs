using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public const int MaxTextLength = 2000;

    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews", table =>
        {
            table.HasCheckConstraint("CK_Reviews_Rating", "[Rating] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_Reviews_FoodRating", "[FoodRating] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_Reviews_ServiceRating", "[ServiceRating] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_Reviews_ValueRating", "[ValueRating] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_Reviews_AtmosphereRating", "[AtmosphereRating] BETWEEN 1 AND 5");
            // A verified review must reference the visit token it was created from (FK added in Phase 5).
            table.HasCheckConstraint(
                "CK_Reviews_VerifiedRequiresVisitToken",
                "[IsVerified] = 0 OR [VisitTokenId] IS NOT NULL");
        });

        builder.HasKey(review => review.Id);

        builder.Property(review => review.Text)
            .HasMaxLength(MaxTextLength)
            .IsRequired();

        builder.Property(review => review.IsVerified)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(review => review.CreatedAt)
            .IsRequired();

        builder.HasOne(review => review.Restaurant)
            .WithMany(restaurant => restaurant.Reviews)
            .HasForeignKey(review => review.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Domain has no navigation to the Identity user; the relationship lives here.
        // Restrict: deleting an account must not silently remove its reviews.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(review => review.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Phase 5: a review references the visit token it was created from.
        // Restrict: a redeemed token cannot be deleted while its review exists.
        builder.HasOne<VisitToken>()
            .WithMany()
            .HasForeignKey(review => review.VisitTokenId)
            .OnDelete(DeleteBehavior.Restrict);

        // At most one review per visit token, enforced by the database (last guard against races).
        builder.HasIndex(review => review.VisitTokenId)
            .IsUnique()
            .HasFilter("[VisitTokenId] IS NOT NULL");

        builder.HasIndex(review => new { review.RestaurantId, review.IsVerified, review.CreatedAt });
        builder.HasIndex(review => review.UserId);
    }
}
