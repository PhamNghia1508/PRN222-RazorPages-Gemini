using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN222.DAL.Entities;

namespace PRN222.DAL.Data.Configurations;

public class KnowledgeAuditLogConfiguration : IEntityTypeConfiguration<KnowledgeAuditLog>
{
    public void Configure(EntityTypeBuilder<KnowledgeAuditLog> builder)
    {
        builder.HasKey(log => log.Id);

        builder.Property(log => log.TriggeredQuestion)
            .IsRequired();

        builder.Property(log => log.SuggestedAnswer)
            .IsRequired();

        builder.Property(log => log.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(log => log.UpdatedByUserId)
            .IsRequired();

        // Foreign keys and relationships
        builder.HasOne(log => log.Course)
            .WithMany()
            .HasForeignKey(log => log.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(log => log.Message)
            .WithMany()
            .HasForeignKey(log => log.MessageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(log => log.UpdatedByUser)
            .WithMany()
            .HasForeignKey(log => log.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(log => log.ApprovedByUser)
            .WithMany()
            .HasForeignKey(log => log.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(log => log.CreatedChunk)
            .WithMany()
            .HasForeignKey(log => log.CreatedChunkId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
