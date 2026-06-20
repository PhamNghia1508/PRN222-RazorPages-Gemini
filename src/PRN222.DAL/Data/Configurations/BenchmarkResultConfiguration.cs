using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN222.DAL.Entities;

namespace PRN222.DAL.Data.Configurations;

public class BenchmarkResultConfiguration : IEntityTypeConfiguration<BenchmarkResult>
{
    public void Configure(EntityTypeBuilder<BenchmarkResult> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Question)
            .IsRequired();

        builder.Property(r => r.GroundTruth)
            .IsRequired();

        builder.Property(r => r.GeneratedAnswer)
            .IsRequired();

        builder.HasOne(r => r.BenchmarkRun)
            .WithMany(b => b.Results)
            .HasForeignKey(r => r.BenchmarkRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.BenchmarkRunId);
    }
}
