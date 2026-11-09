using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class SavedReviewConfiguration : IEntityTypeConfiguration<SavedReview>
{
    public void Configure(EntityTypeBuilder<SavedReview> builder)
    {
        builder.ToTable("SavedReviews");
        builder.HasKey(saved => new { saved.UserId, saved.ReviewId });
        builder.Property(saved => saved.CreatedAt).IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(saved => saved.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Review>()
            .WithMany()
            .HasForeignKey(saved => saved.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(saved => new { saved.UserId, saved.CreatedAt })
            .IsDescending(false, true);
        builder.HasIndex(saved => saved.ReviewId);
    }
}
