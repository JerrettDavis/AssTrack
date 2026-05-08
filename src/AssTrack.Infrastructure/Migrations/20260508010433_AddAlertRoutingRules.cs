using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertRoutingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertRoutingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    IntegrationFeedId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExternalPeerId = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Recipient = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    MessageTemplate = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRoutingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertRoutingRules_IntegrationFeeds_IntegrationFeedId",
                        column: x => x.IntegrationFeedId,
                        principalTable: "IntegrationFeeds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertRoutingRules_IntegrationFeedId",
                table: "AlertRoutingRules",
                column: "IntegrationFeedId");

            migrationBuilder.CreateIndex(
                name: "IX_AlertRoutingRules_IsEnabled_EventType",
                table: "AlertRoutingRules",
                columns: new[] { "IsEnabled", "EventType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertRoutingRules");
        }
    }
}