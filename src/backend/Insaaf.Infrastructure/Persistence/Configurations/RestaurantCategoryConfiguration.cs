using Insaaf.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class RestaurantCategoryConfiguration : IEntityTypeConfiguration<RestaurantCategory>
{
    public void Configure(EntityTypeBuilder<RestaurantCategory> builder)
    {
        builder.ToTable("RestaurantCategories");

        builder.HasKey(join => new { join.RestaurantId, join.CategoryId });

        builder.HasOne(join => join.Restaurant)
            .WithMany(restaurant => restaurant.RestaurantCategories)
            .HasForeignKey(join => join.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(join => join.Category)
            .WithMany(category => category.RestaurantCategories)
            .HasForeignKey(join => join.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(join => join.CategoryId);
    }
}
