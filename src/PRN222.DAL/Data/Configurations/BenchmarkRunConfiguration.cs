using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN222.DAL.Entities;

namespace PRN222.DAL.Data.Configurations;

public class BenchmarkRunConfiguration : IEntityTypeConfiguration<BenchmarkRun>
{
    public void Configure(EntityTypeBuilder<BenchmarkRun> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.ChunkingStrategy)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(b => b.ExperimentType)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("RAG");

        builder.Property(b => b.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasOne(b => b.EmbeddingModel)
            .WithMany()
            .HasForeignKey(b => b.EmbeddingModelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Course)
            .WithMany()
            .HasForeignKey(b => b.CourseId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
