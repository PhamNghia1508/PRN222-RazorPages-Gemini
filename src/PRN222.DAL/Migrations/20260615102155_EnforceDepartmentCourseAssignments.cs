using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222.DAL.Migrations
{
    /// <inheritdoc />
    public partial class EnforceDepartmentCourseAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE assignment
                FROM ApplicationUserCourses AS assignment
                INNER JOIN AspNetUsers AS staff ON staff.Id = assignment.UserId
                INNER JOIN Courses AS course ON course.Id = assignment.CourseId
                WHERE staff.DepartmentId IS NULL
                   OR course.DepartmentId IS NULL
                   OR staff.DepartmentId <> course.DepartmentId;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Invalid authorization assignments cannot be reconstructed safely.
        }
    }
}
