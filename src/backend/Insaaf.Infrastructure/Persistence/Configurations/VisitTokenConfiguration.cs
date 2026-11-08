using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class VisitTokenConfiguration : IEntityTypeConfiguration<VisitToken>
{
    public void Configure(EntityTypeBuilder<VisitToken> builder)
    {
        builder.ToTable("VisitTokens", table =>
        {
            table.HasCheckConstraint("CK_VisitTokens_ExpiresAfterIssued", "[ExpiresAt] > [IssuedAt]");
            table.HasCheckConstraint(
                "CK_VisitTokens_RedeemerRequiresRedemption",
                "[RedeemedByUserId] IS NULL OR [RedeemedAt] IS NOT NULL");
            // Manual (Admin) issuance always records who issued the token.
            table.HasCheckConstraint(
                "CK_VisitTokens_ManualRequiresIssuer",
                "[IssuanceChannel] <> 1 OR [IssuedByUserId] IS NOT NULL");
            // Phase 9A: at most 5 unattached photos per token.
            table.HasCheckConstraint("CK_VisitTokens_PhotoSlotsUsed", "[PhotoSlotsUsed] BETWEEN 0 AND 5");
        });

        builder.HasKey(token => token.Id);

        // SHA-256 hex of the secret token; the secret itself is never stored.
        builder.Property(token => token.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(token => token.TokenHash).IsUnique();

        builder.Property(token => token.IssuanceChannel)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(token => token.IssuedAt).IsRequired();
        builder.Property(token => token.ExpiresAt).IsRequired();

        // Optimistic concurrency: a redemption saves only if the stamp it read is still current.
        builder.Property(token => token.ConcurrencyStamp)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasOne(token => token.Restaurant)
            .WithMany(restaurant => restaurant.VisitTokens)
            .HasForeignKey(token => token.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(token => token.IssuedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(token => token.RedeemedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Phase 9A. No FK on the claim, like the other moderation ids.
        builder.Property(token => token.PhotoSlotsUsed)
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasIndex(token => token.IssuanceId);
        builder.HasIndex(token => new { token.RestaurantId, token.RedeemedAt, token.RevokedAt });
    }
}
