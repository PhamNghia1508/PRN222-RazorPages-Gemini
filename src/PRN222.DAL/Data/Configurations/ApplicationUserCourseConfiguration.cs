using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN222.DAL.Entities;

namespace PRN222.DAL.Data.Configurations;

public class ApplicationUserCourseConfiguration : IEntityTypeConfiguration<ApplicationUserCourse>
{
    public void Configure(EntityTypeBuilder<ApplicationUserCourse> builder)
    {
        builder.HasKey(assignment => new { assignment.UserId, assignment.CourseId });

        builder.Property(assignment => assignment.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasOne(assignment => assignment.User)
            .WithMany(user => user.CourseAssignments)
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(assignment => assignment.Course)
            .WithMany(course => course.UserAssignments)
            .HasForeignKey(assignment => assignment.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(assignment => assignment.CourseId);
    }
}
