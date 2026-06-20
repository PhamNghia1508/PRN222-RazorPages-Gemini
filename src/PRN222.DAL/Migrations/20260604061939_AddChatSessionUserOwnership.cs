using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddChatSessionUserOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "ChatSessions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE ChatSessions
                SET UserId = (SELECT TOP 1 Id FROM AspNetUsers ORDER BY Id)
                WHERE UserId IS NULL
                """);

            migrationBuilder.Sql("DELETE FROM ChatSessions WHERE UserId IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ChatSessions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_UserId_LastMessageAt",
                table: "ChatSessions",
                columns: new[] { "UserId", "LastMessageAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChatSessions_AspNetUsers_UserId",
                table: "ChatSessions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatSessions_AspNetUsers_UserId",
                table: "ChatSessions");

            migrationBuilder.DropIndex(
                name: "IX_ChatSessions_UserId_LastMessageAt",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ChatSessions");
        }
    }
}
