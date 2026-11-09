using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class UserFoodPreferenceConfiguration : IEntityTypeConfiguration<UserFoodPreference>
{
    public void Configure(EntityTypeBuilder<UserFoodPreference> builder)
    {
        builder.ToTable("UserFoodPreferences");
        builder.HasKey(preference => new { preference.UserId, preference.CategoryId });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(preference => preference.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(preference => preference.CategoryId);
    }
}
