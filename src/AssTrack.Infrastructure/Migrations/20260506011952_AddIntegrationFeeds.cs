using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationFeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Devices",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IntegrationFeedId",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "Devices",
                type: "TEXT",
                maxLength: 80,
                nullable: false,
                defaultValue: "manual");

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Devices",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IntegrationFeeds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoCreateDevices = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultTags = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ConfigurationJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationFeeds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_IntegrationFeedId_ExternalId",
                table: "Devices",
                columns: new[] { "IntegrationFeedId", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationFeeds_Provider",
                table: "IntegrationFeeds",
                column: "Provider");

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_IntegrationFeeds_IntegrationFeedId",
                table: "Devices",
                column: "IntegrationFeedId",
                principalTable: "IntegrationFeeds",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_IntegrationFeeds_IntegrationFeedId",
                table: "Devices");

            migrationBuilder.DropTable(
                name: "IntegrationFeeds");

            migrationBuilder.DropIndex(
                name: "IX_Devices_IntegrationFeedId_ExternalId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "IntegrationFeedId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Devices");
        }
    }
}
