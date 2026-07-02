using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentArchiveAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "Documents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedByUserId",
                table: "Documents",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedFromStatus",
                table: "Documents",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ArchivedByUserId",
                table: "Documents",
                column: "ArchivedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_AspNetUsers_ArchivedByUserId",
                table: "Documents",
                column: "ArchivedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_AspNetUsers_ArchivedByUserId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ArchivedByUserId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ArchivedFromStatus",
                table: "Documents");
        }
    }
}
