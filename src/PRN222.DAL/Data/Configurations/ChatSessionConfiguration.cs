using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN222.DAL.Entities;

namespace PRN222.DAL.Data.Configurations;

public class ChatSessionConfiguration : IEntityTypeConfiguration<ChatSession>
{
    public void Configure(EntityTypeBuilder<ChatSession> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.SessionTitle)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(s => s.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasIndex(s => new { s.UserId, s.LastMessageAt });

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
