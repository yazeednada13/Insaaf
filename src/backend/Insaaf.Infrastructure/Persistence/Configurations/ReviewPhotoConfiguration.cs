using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class ReviewPhotoConfiguration : IEntityTypeConfiguration<ReviewPhoto>
{
    public const int MaxStorageKeyLength = 200;

    public void Configure(EntityTypeBuilder<ReviewPhoto> builder)
    {
        builder.ToTable("ReviewPhotos", table =>
        {
            table.HasCheckConstraint("CK_ReviewPhotos_Status", "[Status] BETWEEN 0 AND 3");
            table.HasCheckConstraint("CK_ReviewPhotos_SortOrder", "[SortOrder] IS NULL OR [SortOrder] BETWEEN 0 AND 4");
            // A Ready photo always names its served file.
            table.HasCheckConstraint("CK_ReviewPhotos_ReadyHasFile", "[Status] <> 1 OR [StorageKey] IS NOT NULL");
            // An attached photo always has its position.
            table.HasCheckConstraint("CK_ReviewPhotos_AttachedHasOrder", "[ReviewId] IS NULL OR [SortOrder] IS NOT NULL");
        });

        builder.HasKey(photo => photo.Id);

        builder.Property(photo => photo.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(photo => photo.StorageKey)
            .HasMaxLength(MaxStorageKeyLength);

        builder.Property(photo => photo.ContentType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(photo => photo.CreatedAt).IsRequired();
        builder.Property(photo => photo.UpdatedAt).IsRequired();

        // Every relationship is Restrict: no database delete may remove a photo row without the storage
        // workflow, which deletes the photo's folder first (Phase 9A plan §8).
        builder.HasOne<Review>()
            .WithMany()
            .HasForeignKey(photo => photo.ReviewId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<VisitToken>()
            .WithMany()
            .HasForeignKey(photo => photo.VisitTokenId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(photo => photo.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // DeletedByUserId has no foreign key, like the other moderation ids.

        // Idempotent uploads: one row per logical photo of one user and token.
        builder.HasIndex(photo => new { photo.VisitTokenId, photo.UploadedByUserId, photo.ClientUploadId })
            .IsUnique();

        builder.HasIndex(photo => new { photo.ReviewId, photo.SortOrder });
        builder.HasIndex(photo => new { photo.VisitTokenId, photo.Status });

        // Cleanup: rows by status and age, attached or not. Unfiltered, because SQL Server filtered
        // indexes don't allow OR.
        builder.HasIndex(photo => new { photo.Status, photo.ReviewId, photo.UpdatedAt })
            .HasDatabaseName("IX_ReviewPhotos_Cleanup");
    }
}
