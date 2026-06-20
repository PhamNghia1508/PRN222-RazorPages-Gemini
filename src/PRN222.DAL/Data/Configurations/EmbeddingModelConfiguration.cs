using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN222.DAL.Entities;

namespace PRN222.DAL.Data.Configurations;

public class EmbeddingModelConfiguration : IEntityTypeConfiguration<EmbeddingModel>
{
    public void Configure(EntityTypeBuilder<EmbeddingModel> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(e => e.Name)
            .IsUnique();
    }
}
