using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeRelay.Data.Migrations
{
    /// <inheritdoc />
    public partial class AppUserTableUserNameColumnToEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppUsers_UserName",
                table: "AppUsers");

            migrationBuilder.RenameColumn(
                name: "UserName",
                table: "AppUsers",
                newName: "Email");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "AppUsers",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<int>(
                name: "PersonId",
                table: "AppUsers",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_Email",
                table: "AppUsers",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppUsers_Email",
                table: "AppUsers");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "AppUsers",
                newName: "UserName");

            migrationBuilder.AlterColumn<int>(
                name: "PersonId",
                table: "AppUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "AppUsers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_UserName",
                table: "AppUsers",
                column: "UserName",
                unique: true);
        }
    }
}
