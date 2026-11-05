using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class ReviewLikeConfiguration : IEntityTypeConfiguration<ReviewLike>
{
    public void Configure(EntityTypeBuilder<ReviewLike> builder)
    {
        builder.ToTable("ReviewLikes");

        // One like per user per review, enforced by the database.
        builder.HasKey(like => new { like.ReviewId, like.UserId });

        builder.Property(like => like.CreatedAt)
            .IsRequired();

        builder.HasOne<Review>()
            .WithMany()
            .HasForeignKey(like => like.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, like Reviews: deleting an account must not silently remove its activity.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(like => like.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(like => like.UserId);
    }
}
