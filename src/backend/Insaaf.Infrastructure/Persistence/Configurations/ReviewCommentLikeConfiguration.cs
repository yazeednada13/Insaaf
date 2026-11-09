using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class ReviewCommentLikeConfiguration : IEntityTypeConfiguration<ReviewCommentLike>
{
    public void Configure(EntityTypeBuilder<ReviewCommentLike> builder)
    {
        builder.ToTable("ReviewCommentLikes");
        builder.HasKey(like => new { like.CommentId, like.UserId });
        builder.Property(like => like.CreatedAt).IsRequired();

        // A deleted comment takes its likes with it (one cascade path: Reviews → ReviewComments → here).
        builder.HasOne<ReviewComment>()
            .WithMany()
            .HasForeignKey(like => like.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(like => like.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(like => like.UserId);
    }
}
