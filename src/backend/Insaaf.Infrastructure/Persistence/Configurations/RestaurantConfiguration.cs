using Insaaf.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class RestaurantConfiguration : IEntityTypeConfiguration<Restaurant>
{
    public void Configure(EntityTypeBuilder<Restaurant> builder)
    {
        builder.ToTable("Restaurants");

        builder.HasKey(restaurant => restaurant.Id);

        builder.Property(restaurant => restaurant.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(restaurant => restaurant.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(restaurant => restaurant.City)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(restaurant => restaurant.Address)
            .HasMaxLength(500);

        builder.Property(restaurant => restaurant.PriceLevel)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(restaurant => restaurant.Phone)
            .HasMaxLength(32);

        builder.Property(restaurant => restaurant.ImageUrl)
            .HasMaxLength(2048);

        builder.Property(restaurant => restaurant.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(restaurant => restaurant.CreatedAt)
            .IsRequired();

        builder.Property(restaurant => restaurant.TimeZoneId)
            .HasMaxLength(64);

        builder.HasIndex(restaurant => restaurant.IsActive);
        builder.HasIndex(restaurant => restaurant.City);
    }
}
