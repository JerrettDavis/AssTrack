using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetCustody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustodianContact",
                table: "Assets",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustodianName",
                table: "Assets",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustodySince",
                table: "Assets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustodyStatus",
                table: "Assets",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "available");

            migrationBuilder.CreateTable(
                name: "CustodyEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    FromCustodianName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ToCustodianName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ToCustodianContact = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustodyEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustodyEvents_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustodyEvents_AssetId_OccurredAt",
                table: "CustodyEvents",
                columns: new[] { "AssetId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustodyEvents_EventType",
                table: "CustodyEvents",
                column: "EventType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustodyEvents");

            migrationBuilder.DropColumn(
                name: "CustodianContact",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CustodianName",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CustodySince",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "CustodyStatus",
                table: "Assets");
        }
    }
}
