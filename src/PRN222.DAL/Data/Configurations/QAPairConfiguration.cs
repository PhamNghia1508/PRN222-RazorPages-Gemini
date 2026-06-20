using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN222.DAL.Entities;

namespace PRN222.DAL.Data.Configurations;

public class QAPairConfiguration : IEntityTypeConfiguration<QAPair>
{
    public void Configure(EntityTypeBuilder<QAPair> builder)
    {
        builder.ToTable("QAPairs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Question)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Answer)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.RelevanceScore)
            .HasDefaultValue(1.0);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        // Relationships
        builder.HasOne(x => x.Course)
            .WithMany()
            .HasForeignKey(x => x.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.DocumentChunk)
            .WithMany()
            .HasForeignKey(x => x.DocumentChunkId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
