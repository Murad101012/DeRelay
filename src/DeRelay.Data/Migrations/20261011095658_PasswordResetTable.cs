using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeRelay.Data.Migrations
{
    /// <inheritdoc />
    public partial class PasswordResetTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PasswordReset",
                columns: table => new
                {
                    hashedLink = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    resetExpire = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    linkExpire = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordReset", x => x.hashedLink);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasswordReset");
        }
    }
}
