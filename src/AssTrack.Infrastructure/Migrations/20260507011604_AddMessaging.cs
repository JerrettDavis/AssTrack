using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    IntegrationFeedId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExternalPeerId = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false, defaultValue: "open"),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageThreads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageThreads_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MessageThreads_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MessageThreads_IntegrationFeeds_IntegrationFeedId",
                        column: x => x.IntegrationFeedId,
                        principalTable: "IntegrationFeeds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MessageEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ThreadId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Direction = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Sender = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Recipient = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Body = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageEntries_MessageThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "MessageThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageEntries_ProviderMessageId",
                table: "MessageEntries",
                column: "ProviderMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageEntries_Status_Direction_CreatedAt",
                table: "MessageEntries",
                columns: new[] { "Status", "Direction", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageEntries_ThreadId_CreatedAt",
                table: "MessageEntries",
                columns: new[] { "ThreadId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreads_AssetId",
                table: "MessageThreads",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreads_DeviceId",
                table: "MessageThreads",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreads_IntegrationFeedId_ExternalPeerId",
                table: "MessageThreads",
                columns: new[] { "IntegrationFeedId", "ExternalPeerId" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreads_LastMessageAt",
                table: "MessageThreads",
                column: "LastMessageAt");

            migrationBuilder.CreateIndex(
                name: "IX_MessageThreads_Provider_ExternalPeerId",
                table: "MessageThreads",
                columns: new[] { "Provider", "ExternalPeerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageEntries");

            migrationBuilder.DropTable(
                name: "MessageThreads");
        }
    }
}
