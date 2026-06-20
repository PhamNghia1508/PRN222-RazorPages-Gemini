using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PRN222.DAL.Data;

#nullable disable

namespace PRN222.DAL.Migrations
{
    public partial class AddBenchmarkRunCourseScope : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[BenchmarkRuns]', N'CourseId') IS NULL
                BEGIN
                    ALTER TABLE [BenchmarkRuns] ADD [CourseId] int NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_BenchmarkRuns_CourseId'
                      AND object_id = OBJECT_ID(N'[BenchmarkRuns]')
                )
                BEGIN
                    CREATE INDEX [IX_BenchmarkRuns_CourseId] ON [BenchmarkRuns] ([CourseId]);
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_BenchmarkRuns_Courses_CourseId'
                      AND parent_object_id = OBJECT_ID(N'[BenchmarkRuns]')
                )
                BEGIN
                    ALTER TABLE [BenchmarkRuns]
                    ADD CONSTRAINT [FK_BenchmarkRuns_Courses_CourseId]
                    FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE SET NULL;
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_BenchmarkRuns_Courses_CourseId'
                      AND parent_object_id = OBJECT_ID(N'[BenchmarkRuns]')
                )
                BEGIN
                    ALTER TABLE [BenchmarkRuns] DROP CONSTRAINT [FK_BenchmarkRuns_Courses_CourseId];
                END
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'IX_BenchmarkRuns_CourseId'
                      AND object_id = OBJECT_ID(N'[BenchmarkRuns]')
                )
                BEGIN
                    DROP INDEX [IX_BenchmarkRuns_CourseId] ON [BenchmarkRuns];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[BenchmarkRuns]', N'CourseId') IS NOT NULL
                BEGIN
                    ALTER TABLE [BenchmarkRuns] DROP COLUMN [CourseId];
                END
                """);
        }
    }
}
