using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddBenchmarkExperimentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExperimentType",
                table: "BenchmarkRuns",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "RAG");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExperimentType",
                table: "BenchmarkRuns");
        }
    }
}
