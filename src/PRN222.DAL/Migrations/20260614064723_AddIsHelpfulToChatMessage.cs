using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddIsHelpfulToChatMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHelpful",
                table: "ChatMessages",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHelpful",
                table: "ChatMessages");
        }
    }
}
