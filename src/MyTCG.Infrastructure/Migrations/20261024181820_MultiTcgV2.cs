using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyTCG.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiTcgV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tcg",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CoverImage = table.Column<string>(type: "text", nullable: true),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    CreatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tcg", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tcg_AspNetUsers_CreatorUserId",
                        column: x => x.CreatorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Card",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TcgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CardNumber = table.Column<string>(type: "text", nullable: false),
                    CardType = table.Column<int>(type: "integer", nullable: false),
                    Rarity = table.Column<int>(type: "integer", nullable: false),
                    ActionCost = table.Column<int>(type: "integer", nullable: false),
                    Attack = table.Column<int>(type: "integer", nullable: true),
                    Defense = table.Column<int>(type: "integer", nullable: true),
                    DropWeight = table.Column<double>(type: "double precision", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Card", x => x.Id);
                    table.UniqueConstraint("AK_Card_TcgId_Id", x => new { x.TcgId, x.Id });
                    table.CheckConstraint("CK_Card_Cost", "\"ActionCost\" BETWEEN 1 AND 12");
                    table.CheckConstraint("CK_Card_Stats", "(\"Attack\" IS NULL OR \"Attack\" >= 0) AND (\"Defense\" IS NULL OR \"Defense\" >= 0)");
                    table.ForeignKey(
                        name: "FK_Card_Tcg_TcgId",
                        column: x => x.TcgId,
                        principalTable: "Tcg",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TcgInvitation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TcgId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    ProtectedCode = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TcgInvitation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TcgInvitation_Tcg_TcgId",
                        column: x => x.TcgId,
                        principalTable: "Tcg",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TcgMembership",
                columns: table => new
                {
                    TcgId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TcgMembership", x => new { x.TcgId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TcgMembership_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TcgMembership_Tcg_TcgId",
                        column: x => x.TcgId,
                        principalTable: "Tcg",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoosterOpening",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TcgId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoosterOpening", x => x.Id);
                    table.UniqueConstraint("AK_BoosterOpening_TcgId_Id", x => new { x.TcgId, x.Id });
                    table.ForeignKey(
                        name: "FK_BoosterOpening_TcgMembership_TcgId_UserId",
                        columns: x => new { x.TcgId, x.UserId },
                        principalTable: "TcgMembership",
                        principalColumns: new[] { "TcgId", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Deck",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TcgId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deck", x => x.Id);
                    table.UniqueConstraint("AK_Deck_TcgId_Id", x => new { x.TcgId, x.Id });
                    table.ForeignKey(
                        name: "FK_Deck_TcgMembership_TcgId_UserId",
                        columns: x => new { x.TcgId, x.UserId },
                        principalTable: "TcgMembership",
                        principalColumns: new[] { "TcgId", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCard",
                columns: table => new
                {
                    TcgId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCard", x => new { x.TcgId, x.UserId, x.CardId });
                    table.CheckConstraint("CK_UserCard_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_UserCard_Card_TcgId_CardId",
                        columns: x => new { x.TcgId, x.CardId },
                        principalTable: "Card",
                        principalColumns: new[] { "TcgId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserCard_TcgMembership_TcgId_UserId",
                        columns: x => new { x.TcgId, x.UserId },
                        principalTable: "TcgMembership",
                        principalColumns: new[] { "TcgId", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoosterOpeningCard",
                columns: table => new
                {
                    BoosterOpeningId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    TcgId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoosterOpeningCard", x => new { x.BoosterOpeningId, x.Position });
                    table.ForeignKey(
                        name: "FK_BoosterOpeningCard_BoosterOpening_TcgId_BoosterOpeningId",
                        columns: x => new { x.TcgId, x.BoosterOpeningId },
                        principalTable: "BoosterOpening",
                        principalColumns: new[] { "TcgId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BoosterOpeningCard_Card_TcgId_CardId",
                        columns: x => new { x.TcgId, x.CardId },
                        principalTable: "Card",
                        principalColumns: new[] { "TcgId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeckCard",
                columns: table => new
                {
                    DeckId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    TcgId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckCard", x => new { x.DeckId, x.CardId });
                    table.CheckConstraint("CK_DeckCard_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_DeckCard_Card_TcgId_CardId",
                        columns: x => new { x.TcgId, x.CardId },
                        principalTable: "Card",
                        principalColumns: new[] { "TcgId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeckCard_Deck_TcgId_DeckId",
                        columns: x => new { x.TcgId, x.DeckId },
                        principalTable: "Deck",
                        principalColumns: new[] { "TcgId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoosterOpening_TcgId_UserId_IdempotencyKey",
                table: "BoosterOpening",
                columns: new[] { "TcgId", "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoosterOpening_TcgId_UserId_OpenedAt",
                table: "BoosterOpening",
                columns: new[] { "TcgId", "UserId", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BoosterOpeningCard_TcgId_BoosterOpeningId",
                table: "BoosterOpeningCard",
                columns: new[] { "TcgId", "BoosterOpeningId" });

            migrationBuilder.CreateIndex(
                name: "IX_BoosterOpeningCard_TcgId_CardId",
                table: "BoosterOpeningCard",
                columns: new[] { "TcgId", "CardId" });

            migrationBuilder.CreateIndex(
                name: "IX_Card_TcgId_CardNumber",
                table: "Card",
                columns: new[] { "TcgId", "CardNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deck_TcgId_UserId",
                table: "Deck",
                columns: new[] { "TcgId", "UserId" },
                unique: true,
                filter: "\"IsPrimary\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_DeckCard_TcgId_CardId",
                table: "DeckCard",
                columns: new[] { "TcgId", "CardId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeckCard_TcgId_DeckId",
                table: "DeckCard",
                columns: new[] { "TcgId", "DeckId" });

            migrationBuilder.CreateIndex(
                name: "IX_Tcg_CreatorUserId",
                table: "Tcg",
                column: "CreatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TcgInvitation_CodeHash",
                table: "TcgInvitation",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TcgInvitation_TcgId",
                table: "TcgInvitation",
                column: "TcgId",
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TcgMembership_UserId",
                table: "TcgMembership",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCard_TcgId_CardId",
                table: "UserCard",
                columns: new[] { "TcgId", "CardId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoosterOpeningCard");

            migrationBuilder.DropTable(
                name: "DeckCard");

            migrationBuilder.DropTable(
                name: "TcgInvitation");

            migrationBuilder.DropTable(
                name: "UserCard");

            migrationBuilder.DropTable(
                name: "BoosterOpening");

            migrationBuilder.DropTable(
                name: "Deck");

            migrationBuilder.DropTable(
                name: "Card");

            migrationBuilder.DropTable(
                name: "TcgMembership");

            migrationBuilder.DropTable(
                name: "Tcg");
        }
    }
}
