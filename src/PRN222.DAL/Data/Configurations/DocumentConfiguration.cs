using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN222.DAL.Entities;

namespace PRN222.DAL.Data.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.OriginalFileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.StoragePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(d => d.ChunkingStrategy)
            .HasMaxLength(50);

        builder.Property(d => d.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(d => d.UploadedByUserId)
            .HasMaxLength(450);

        builder.Property(d => d.ArchiveReason)
            .HasMaxLength(1000);

        builder.Property(d => d.ArchivedFromStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(d => d.CancelledByUserId)
            .HasMaxLength(450);

        builder.Property(d => d.CancellationReason)
            .HasMaxLength(1000);

        // Relationships
        builder.HasOne(d => d.Course)
            .WithMany(c => c.Documents)
            .HasForeignKey(d => d.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.EmbeddingModel)
            .WithMany(e => e.Documents)
            .HasForeignKey(d => d.EmbeddingModelId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.UploadedByUser)
            .WithMany()
            .HasForeignKey(d => d.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.ArchivedByUser)
            .WithMany()
            .HasForeignKey(d => d.ArchivedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.CancelledByUser)
            .WithMany()
            .HasForeignKey(d => d.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(d => d.CourseId);
        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => d.UploadedByUserId);
        builder.HasIndex(d => d.ArchivedByUserId);
        builder.HasIndex(d => d.CancelledByUserId);
    }
}
