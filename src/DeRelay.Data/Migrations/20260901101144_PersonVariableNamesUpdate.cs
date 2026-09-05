using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeRelay.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersonVariableNamesUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Nickname",
                table: "Persons",
                newName: "NickName");

            migrationBuilder.RenameColumn(
                name: "Lastname",
                table: "Persons",
                newName: "LastName");

            migrationBuilder.RenameColumn(
                name: "Firstname",
                table: "Persons",
                newName: "FirstName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NickName",
                table: "Persons",
                newName: "Nickname");

            migrationBuilder.RenameColumn(
                name: "LastName",
                table: "Persons",
                newName: "Lastname");

            migrationBuilder.RenameColumn(
                name: "FirstName",
                table: "Persons",
                newName: "Firstname");
        }
    }
}
