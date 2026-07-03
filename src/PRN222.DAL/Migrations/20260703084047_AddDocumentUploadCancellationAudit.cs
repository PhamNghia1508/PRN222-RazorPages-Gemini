using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PRN222.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentUploadCancellationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Documents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Documents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledByUserId",
                table: "Documents",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CancelledByUserId",
                table: "Documents",
                column: "CancelledByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_AspNetUsers_CancelledByUserId",
                table: "Documents",
                column: "CancelledByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_AspNetUsers_CancelledByUserId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_CancelledByUserId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "Documents");
        }
    }
}
