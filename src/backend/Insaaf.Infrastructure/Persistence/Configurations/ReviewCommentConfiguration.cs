using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class ReviewCommentConfiguration : IEntityTypeConfiguration<ReviewComment>
{
    public const int MaxTextLength = 500;

    public void Configure(EntityTypeBuilder<ReviewComment> builder)
    {
        builder.ToTable("ReviewComments", table =>
        {
            table.HasCheckConstraint("CK_ReviewComments_TextNotBlank", "LEN([Text]) > 0");
        });

        builder.HasKey(comment => comment.Id);

        builder.Property(comment => comment.Text)
            .HasMaxLength(MaxTextLength)
            .IsRequired();

        builder.Property(comment => comment.CreatedAt)
            .IsRequired();

        builder.HasOne<Review>()
            .WithMany()
            .HasForeignKey(comment => comment.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, like Reviews: deleting an account must not silently remove its activity.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(comment => comment.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(comment => new { comment.ReviewId, comment.CreatedAt });
        builder.HasIndex(comment => comment.UserId);
    }
}
