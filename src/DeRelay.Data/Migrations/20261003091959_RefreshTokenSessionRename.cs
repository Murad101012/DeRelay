using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeRelay.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefreshTokenSessionRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FamilyId",
                table: "RefreshToken",
                newName: "SessionId");

            migrationBuilder.RenameColumn(
                name: "FamilyExpiry",
                table: "RefreshToken",
                newName: "SessionExpiry");

            migrationBuilder.RenameIndex(
                name: "IX_RefreshToken_FamilyId",
                table: "RefreshToken",
                newName: "IX_RefreshToken_SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SessionId",
                table: "RefreshToken",
                newName: "FamilyId");

            migrationBuilder.RenameColumn(
                name: "SessionExpiry",
                table: "RefreshToken",
                newName: "FamilyExpiry");

            migrationBuilder.RenameIndex(
                name: "IX_RefreshToken_SessionId",
                table: "RefreshToken",
                newName: "IX_RefreshToken_FamilyId");
        }
    }
}
