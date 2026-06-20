using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN222.DAL.Entities;

namespace PRN222.DAL.Data.Configurations;

public class ChatCitationConfiguration : IEntityTypeConfiguration<ChatCitation>
{
    public void Configure(EntityTypeBuilder<ChatCitation> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.SnippetText)
            .HasMaxLength(1000);

        builder.HasOne(c => c.Message)
            .WithMany(m => m.Citations)
            .HasForeignKey(c => c.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Chunk)
            .WithMany()
            .HasForeignKey(c => c.ChunkId)
            .OnDelete(DeleteBehavior.NoAction); // Avoid multiple cascade paths

        builder.HasIndex(c => c.MessageId);
    }
}
