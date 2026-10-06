using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeRelay.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefreshTokenServiceChainNumberColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChainNumber",
                table: "RefreshToken",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChainNumber",
                table: "RefreshToken");
        }
    }
}
