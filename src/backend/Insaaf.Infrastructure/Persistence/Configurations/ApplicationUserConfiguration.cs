using Insaaf.Domain.Enums;
using Insaaf.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public const int MaxBioLength = 150;
    public const int MaxAvatarKeyLength = 100;

    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.FullName)
            .HasMaxLength(256);

        builder.Property(user => user.Bio)
            .HasMaxLength(MaxBioLength);

        // Phase 11.
        builder.Property(user => user.AvatarKey)
            .HasMaxLength(MaxAvatarKeyLength);

        builder.Property(user => user.TastePriorities)
            .HasConversion<int>()
            .HasDefaultValue(TastePriority.None);

        // Existing accounts keep every notification on.
        builder.Property(user => user.NotifyLikes).HasDefaultValue(true);
        builder.Property(user => user.NotifyComments).HasDefaultValue(true);
        builder.Property(user => user.NotifyFollows).HasDefaultValue(true);
    }
}
