using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseScopeToChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CourseId",
                table: "ChatMessages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_CourseId",
                table: "ChatMessages",
                column: "CourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Courses_CourseId",
                table: "ChatMessages",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Courses_CourseId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_CourseId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "ChatMessages");
        }
    }
}
