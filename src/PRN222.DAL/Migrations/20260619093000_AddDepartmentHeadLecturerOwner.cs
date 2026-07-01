using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace PRN222.DAL.Migrations
{
    [DbContext(typeof(PRN222.DAL.Data.ChatbotDbContext))]
    [Migration("20260619093000_AddDepartmentHeadLecturerOwner")]
    /// <inheritdoc />
    public partial class AddDepartmentHeadLecturerOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeadLecturerUserId",
                table: "Departments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE department
                SET HeadLecturerUserId = selected.HeadLecturerUserId
                FROM Departments AS department
                CROSS APPLY (
                    SELECT TOP 1 staff.Id AS HeadLecturerUserId
                    FROM AspNetUsers AS staff
                    INNER JOIN AspNetUserRoles AS userRole ON userRole.UserId = staff.Id
                    INNER JOIN AspNetRoles AS role ON role.Id = userRole.RoleId
                    WHERE staff.DepartmentId = department.Id
                      AND role.Name = N'HeadLecturer'
                    ORDER BY staff.Email, staff.Id
                ) AS selected;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_HeadLecturerUserId",
                table: "Departments",
                column: "HeadLecturerUserId",
                unique: true,
                filter: "[HeadLecturerUserId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_AspNetUsers_HeadLecturerUserId",
                table: "Departments",
                column: "HeadLecturerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_AspNetUsers_HeadLecturerUserId",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Departments_HeadLecturerUserId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "HeadLecturerUserId",
                table: "Departments");
        }
    }
}
