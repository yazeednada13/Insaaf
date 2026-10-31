using Insaaf.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class RestaurantOpeningHoursConfiguration : IEntityTypeConfiguration<RestaurantOpeningHours>
{
    public void Configure(EntityTypeBuilder<RestaurantOpeningHours> builder)
    {
        builder.ToTable("RestaurantOpeningHours");

        builder.HasKey(hours => hours.Id);

        builder.Property(hours => hours.DayOfWeek)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(hours => hours.OpensAt).IsRequired();

        builder.Property(hours => hours.ClosesAt).IsRequired();

        builder.HasOne(hours => hours.Restaurant)
            .WithMany(restaurant => restaurant.OpeningHours)
            .HasForeignKey(hours => hours.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(hours => new { hours.RestaurantId, hours.DayOfWeek });
    }
}
