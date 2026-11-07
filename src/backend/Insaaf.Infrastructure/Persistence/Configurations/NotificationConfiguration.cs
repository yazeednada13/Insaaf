using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", table =>
        {
            table.HasCheckConstraint("CK_Notifications_NotSelf", "[RecipientId] <> [ActorId]");
        });

        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(notification => notification.CreatedAt)
            .IsRequired();

        // Restrict on both users: two cascades from AspNetUsers into one table are refused.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(notification => notification.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(notification => notification.ActorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Review>()
            .WithMany()
            .HasForeignKey(notification => notification.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        // CommentId has no foreign key: Reviews → ReviewComments → Notifications would be a second
        // cascade path from Reviews. The comment service deletes the notification with the comment.

        // The recipient's list, newest first, with a stable tie-breaker.
        builder.HasIndex(notification => new { notification.RecipientId, notification.CreatedAt, notification.Id })
            .IsDescending(false, true, true);

        // The unread count.
        builder.HasIndex(notification => notification.RecipientId)
            .HasFilter("[ReadAt] IS NULL")
            .HasDatabaseName("IX_Notifications_RecipientId_Unread");

        builder.HasIndex(notification => notification.ActorId);
        builder.HasIndex(notification => notification.ReviewId);
    }
}
