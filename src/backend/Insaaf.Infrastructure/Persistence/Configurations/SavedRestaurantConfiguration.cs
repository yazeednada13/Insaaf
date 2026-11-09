using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class SavedRestaurantConfiguration : IEntityTypeConfiguration<SavedRestaurant>
{
    public void Configure(EntityTypeBuilder<SavedRestaurant> builder)
    {
        builder.ToTable("SavedRestaurants");
        builder.HasKey(saved => new { saved.UserId, saved.RestaurantId });
        builder.Property(saved => saved.CreatedAt).IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(saved => saved.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restaurants are never hard-deleted (Phase 9); Restrict keeps it that way.
        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(saved => saved.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict);

        // The owner's list, newest first.
        builder.HasIndex(saved => new { saved.UserId, saved.CreatedAt })
            .IsDescending(false, true);
        builder.HasIndex(saved => saved.RestaurantId);
    }
}
