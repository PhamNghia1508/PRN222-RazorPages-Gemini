using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PRN222.DAL.Data;

#nullable disable

namespace PRN222.DAL.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ChatbotDbContext))]
    [Migration("20260625093000_RepairDepartmentHeadLecturerOwnerSchema")]
    public partial class RepairDepartmentHeadLecturerOwnerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Departments', N'HeadLecturerUserId') IS NULL
                BEGIN
                    ALTER TABLE [Departments] ADD [HeadLecturerUserId] nvarchar(450) NULL;
                END
                """);

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
                ) AS selected
                WHERE department.HeadLecturerUserId IS NULL;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Departments_HeadLecturerUserId'
                      AND object_id = OBJECT_ID(N'[dbo].[Departments]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_Departments_HeadLecturerUserId]
                    ON [Departments] ([HeadLecturerUserId])
                    WHERE [HeadLecturerUserId] IS NOT NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_Departments_AspNetUsers_HeadLecturerUserId'
                      AND parent_object_id = OBJECT_ID(N'[dbo].[Departments]')
                )
                BEGIN
                    ALTER TABLE [Departments]
                    ADD CONSTRAINT [FK_Departments_AspNetUsers_HeadLecturerUserId]
                    FOREIGN KEY ([HeadLecturerUserId]) REFERENCES [AspNetUsers] ([Id]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_Departments_AspNetUsers_HeadLecturerUserId'
                      AND parent_object_id = OBJECT_ID(N'[dbo].[Departments]')
                )
                BEGIN
                    ALTER TABLE [Departments]
                    DROP CONSTRAINT [FK_Departments_AspNetUsers_HeadLecturerUserId];
                END
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_Departments_HeadLecturerUserId'
                      AND object_id = OBJECT_ID(N'[dbo].[Departments]')
                )
                BEGIN
                    DROP INDEX [IX_Departments_HeadLecturerUserId] ON [Departments];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Departments', N'HeadLecturerUserId') IS NOT NULL
                BEGIN
                    ALTER TABLE [Departments] DROP COLUMN [HeadLecturerUserId];
                END
                """);
        }
    }
}
