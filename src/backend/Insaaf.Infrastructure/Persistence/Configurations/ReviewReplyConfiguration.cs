using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class ReviewReplyConfiguration : IEntityTypeConfiguration<ReviewReply>
{
    public const int MaxTextLength = 1000;

    public void Configure(EntityTypeBuilder<ReviewReply> builder)
    {
        builder.ToTable("ReviewReplies");
        builder.HasKey(reply => reply.ReviewId);

        builder.Property(reply => reply.Text).HasMaxLength(MaxTextLength).IsRequired();
        builder.Property(reply => reply.CreatedAt).IsRequired();
        builder.Property(reply => reply.UpdatedAt).IsRequired();

        builder.HasOne<Review>()
            .WithOne()
            .HasForeignKey<ReviewReply>(reply => reply.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        // WrittenByUserId has no foreign key (ADR-017), like the other moderation actors.
    }
}
