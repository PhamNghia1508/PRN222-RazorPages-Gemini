using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN222.DAL.Entities;

namespace PRN222.DAL.Data.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Content)
            .IsRequired();

        builder.Property(m => m.Role)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne(m => m.Session)
            .WithMany(s => s.Messages)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Course)
            .WithMany()
            .HasForeignKey(m => m.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.SessionId);
        builder.HasIndex(m => m.CourseId);
    }
}
