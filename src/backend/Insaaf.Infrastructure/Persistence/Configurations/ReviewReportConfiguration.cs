using Insaaf.Domain.Entities;
using Insaaf.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Insaaf.Infrastructure.Persistence.Configurations;

public sealed class ReviewReportConfiguration : IEntityTypeConfiguration<ReviewReport>
{
    public const int MaxNoteLength = 500;

    public void Configure(EntityTypeBuilder<ReviewReport> builder)
    {
        builder.ToTable("ReviewReports");
        builder.HasKey(report => report.Id);

        builder.Property(report => report.Reason).HasConversion<int>().IsRequired();
        builder.Property(report => report.Note).HasMaxLength(MaxNoteLength);
        builder.Property(report => report.CreatedAt).IsRequired();

        builder.HasOne<Review>()
            .WithMany()
            .HasForeignKey(report => report.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(report => report.ReporterUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // One open report per user per review (a single IS NULL filter, valid on SQL Server).
        builder.HasIndex(report => new { report.ReviewId, report.ReporterUserId })
            .IsUnique()
            .HasFilter("[ResolvedAt] IS NULL")
            .HasDatabaseName("IX_ReviewReports_Open_PerReporter");

        // The Admin queue: open reports, oldest first.
        builder.HasIndex(report => new { report.ResolvedAt, report.CreatedAt });
        builder.HasIndex(report => report.ReporterUserId);
    }
}
