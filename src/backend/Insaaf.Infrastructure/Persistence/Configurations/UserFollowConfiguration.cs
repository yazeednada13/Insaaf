using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class UserFollowConfiguration : IEntityTypeConfiguration<UserFollow>
{
    public void Configure(EntityTypeBuilder<UserFollow> builder)
    {
        builder.ToTable("UserFollows", table =>
        {
            table.HasCheckConstraint("CK_UserFollows_NotSelf", "[FollowerId] <> [FolloweeId]");
        });

        // One follow per pair, enforced by the database.
        builder.HasKey(follow => new { follow.FollowerId, follow.FolloweeId });

        builder.Property(follow => follow.CreatedAt)
            .IsRequired();

        // Restrict on both sides: two cascades into one table are refused by SQL Server, and deleting
        // an account must not silently remove its activity.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(follow => follow.FollowerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(follow => follow.FolloweeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(follow => follow.FolloweeId);
    }
}
