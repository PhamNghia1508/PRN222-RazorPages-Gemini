using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN222.DAL.Entities;

namespace PRN222.DAL.Data.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(d => d.Description)
            .HasMaxLength(1000);

        builder.Property(d => d.HeadLecturerUserId)
            .HasMaxLength(450);

        builder.HasIndex(d => d.Code)
            .IsUnique();

        builder.HasIndex(d => d.HeadLecturerUserId)
            .IsUnique()
            .HasFilter("[HeadLecturerUserId] IS NOT NULL");

        builder.HasOne(d => d.HeadLecturer)
            .WithMany()
            .HasForeignKey(d => d.HeadLecturerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        // ApplicationUser -> Department: SetNull so deleting a dept doesn't delete users
        builder.HasMany(d => d.Users)
            .WithOne(u => u.Department)
            .HasForeignKey(u => u.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Course -> Department: SetNull so deleting a dept doesn't delete courses
        builder.HasMany(d => d.Courses)
            .WithOne(c => c.Department)
            .HasForeignKey(c => c.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
