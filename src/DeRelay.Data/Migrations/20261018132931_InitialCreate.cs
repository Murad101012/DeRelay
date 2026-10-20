using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DeRelay.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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

            migrationBuilder.CreateTable(
                name: "PendingRegistrations",
                columns: table => new
                {
                    Hash = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    HashedPassword = table.Column<string>(type: "text", nullable: false),
                    ConfirmationExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LinkExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingRegistrations", x => x.Hash);
                });

            migrationBuilder.CreateTable(
                name: "Persons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FirstName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NickName = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Gender = table.Column<int>(type: "integer", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Persons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppUsers_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FriendRequests",
                columns: table => new
                {
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    ReceiverId = table.Column<int>(type: "integer", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendRequests", x => new { x.SenderId, x.ReceiverId });
                    table.ForeignKey(
                        name: "FK_FriendRequests_Persons_ReceiverId",
                        column: x => x.ReceiverId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FriendRequests_Persons_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Friendships",
                columns: table => new
                {
                    User1Id = table.Column<int>(type: "integer", nullable: false),
                    User2Id = table.Column<int>(type: "integer", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Friendships", x => new { x.User1Id, x.User2Id });
                    table.ForeignKey(
                        name: "FK_Friendships_Persons_User1Id",
                        column: x => x.User1Id,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Friendships_Persons_User2Id",
                        column: x => x.User2Id,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshToken",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppUserId = table.Column<int>(type: "integer", nullable: false),
                    HashedToken = table.Column<string>(type: "text", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ChainNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshToken", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshToken_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_Email",
                table: "AppUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_PersonId",
                table: "AppUsers",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_ReceiverId",
                table: "FriendRequests",
                column: "ReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_User2Id",
                table: "Friendships",
                column: "User2Id");

            migrationBuilder.CreateIndex(
                name: "IX_PendingRegistrations_Email",
                table: "PendingRegistrations",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Persons_NickName",
                table: "Persons",
                column: "NickName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_AppUserId",
                table: "RefreshToken",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_HashedToken",
                table: "RefreshToken",
                column: "HashedToken");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_SessionId",
                table: "RefreshToken",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FriendRequests");

            migrationBuilder.DropTable(
                name: "Friendships");

            migrationBuilder.DropTable(
                name: "PasswordReset");

            migrationBuilder.DropTable(
                name: "PendingRegistrations");

            migrationBuilder.DropTable(
                name: "RefreshToken");

            migrationBuilder.DropTable(
                name: "AppUsers");

            migrationBuilder.DropTable(
                name: "Persons");
        }
    }
}
