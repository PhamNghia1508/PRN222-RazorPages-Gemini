using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN222.DAL.Entities;

namespace PRN222.DAL.Data.Configurations;

public class ChunkEmbeddingConfiguration : IEntityTypeConfiguration<ChunkEmbedding>
{
    public void Configure(EntityTypeBuilder<ChunkEmbedding> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EmbeddingModelName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.EmbeddingVector)
            .IsRequired();

        builder.HasOne(e => e.Chunk)
            .WithMany(c => c.Embeddings)
            .HasForeignKey(e => e.ChunkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.ChunkId, e.EmbeddingModelName });
    }
}
