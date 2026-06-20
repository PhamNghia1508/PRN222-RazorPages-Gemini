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

        // Relationships
        builder.HasOne(d => d.Course)
            .WithMany(c => c.Documents)
            .HasForeignKey(d => d.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.EmbeddingModel)
            .WithMany(e => e.Documents)
            .HasForeignKey(d => d.EmbeddingModelId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(d => d.CourseId);
        builder.HasIndex(d => d.Status);
    }
}
