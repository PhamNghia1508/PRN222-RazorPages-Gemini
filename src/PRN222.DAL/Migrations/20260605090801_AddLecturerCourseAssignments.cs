using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddLecturerCourseAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ApplicationUserCourses]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [ApplicationUserCourses] (
                        [UserId] nvarchar(450) NOT NULL,
                        [CourseId] int NOT NULL,
                        [AssignedAt] datetime2 NOT NULL,
                        CONSTRAINT [PK_ApplicationUserCourses] PRIMARY KEY ([UserId], [CourseId]),
                        CONSTRAINT [FK_ApplicationUserCourses_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_ApplicationUserCourses_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE
                    );
                END;

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_ApplicationUserCourses_CourseId'
                        AND object_id = OBJECT_ID(N'[ApplicationUserCourses]', N'U')
                )
                BEGIN
                    CREATE INDEX [IX_ApplicationUserCourses_CourseId]
                    ON [ApplicationUserCourses] ([CourseId]);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[ApplicationUserCourses]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [ApplicationUserCourses];
                END;
                """);
        }
    }
}
