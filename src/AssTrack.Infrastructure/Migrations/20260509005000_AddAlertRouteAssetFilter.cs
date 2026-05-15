using System;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AssTrackDbContext))]
    [Migration("20260509005000_AddAlertRouteAssetFilter")]
    public partial class AddAlertRouteAssetFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                PRAGMA foreign_keys = 0;

                CREATE TABLE "AlertRoutingRules_new" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_AlertRoutingRules" PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "IsEnabled" INTEGER NOT NULL,
                    "EventType" TEXT NOT NULL,
                    "Channel" TEXT NOT NULL,
                    "Provider" TEXT NOT NULL,
                    "AssetId" TEXT NULL,
                    "IntegrationFeedId" TEXT NULL,
                    "ExternalPeerId" TEXT NULL,
                    "DisplayName" TEXT NULL,
                    "Recipient" TEXT NULL,
                    "MessageTemplate" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_AlertRoutingRules_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES "Assets" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_AlertRoutingRules_IntegrationFeeds_IntegrationFeedId" FOREIGN KEY ("IntegrationFeedId") REFERENCES "IntegrationFeeds" ("Id") ON DELETE SET NULL
                );

                INSERT INTO "AlertRoutingRules_new" (
                    "Id", "Name", "IsEnabled", "EventType", "Channel", "Provider", "AssetId",
                    "IntegrationFeedId", "ExternalPeerId", "DisplayName", "Recipient", "MessageTemplate",
                    "CreatedAt", "UpdatedAt")
                SELECT
                    "Id", "Name", "IsEnabled", "EventType", "Channel", "Provider", NULL,
                    "IntegrationFeedId", "ExternalPeerId", "DisplayName", "Recipient", "MessageTemplate",
                    "CreatedAt", "UpdatedAt"
                FROM "AlertRoutingRules";

                DROP TABLE "AlertRoutingRules";
                ALTER TABLE "AlertRoutingRules_new" RENAME TO "AlertRoutingRules";

                CREATE INDEX "IX_AlertRoutingRules_AssetId" ON "AlertRoutingRules" ("AssetId");
                CREATE INDEX "IX_AlertRoutingRules_IntegrationFeedId" ON "AlertRoutingRules" ("IntegrationFeedId");
                CREATE INDEX "IX_AlertRoutingRules_IsEnabled_EventType" ON "AlertRoutingRules" ("IsEnabled", "EventType");

                PRAGMA foreign_keys = 1;
                """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                PRAGMA foreign_keys = 0;

                CREATE TABLE "AlertRoutingRules_old" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_AlertRoutingRules" PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "IsEnabled" INTEGER NOT NULL,
                    "EventType" TEXT NOT NULL,
                    "Channel" TEXT NOT NULL,
                    "Provider" TEXT NOT NULL,
                    "IntegrationFeedId" TEXT NULL,
                    "ExternalPeerId" TEXT NULL,
                    "DisplayName" TEXT NULL,
                    "Recipient" TEXT NULL,
                    "MessageTemplate" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_AlertRoutingRules_IntegrationFeeds_IntegrationFeedId" FOREIGN KEY ("IntegrationFeedId") REFERENCES "IntegrationFeeds" ("Id") ON DELETE SET NULL
                );

                INSERT INTO "AlertRoutingRules_old" (
                    "Id", "Name", "IsEnabled", "EventType", "Channel", "Provider",
                    "IntegrationFeedId", "ExternalPeerId", "DisplayName", "Recipient", "MessageTemplate",
                    "CreatedAt", "UpdatedAt")
                SELECT
                    "Id", "Name", "IsEnabled", "EventType", "Channel", "Provider",
                    "IntegrationFeedId", "ExternalPeerId", "DisplayName", "Recipient", "MessageTemplate",
                    "CreatedAt", "UpdatedAt"
                FROM "AlertRoutingRules";

                DROP TABLE "AlertRoutingRules";
                ALTER TABLE "AlertRoutingRules_old" RENAME TO "AlertRoutingRules";

                CREATE INDEX "IX_AlertRoutingRules_IntegrationFeedId" ON "AlertRoutingRules" ("IntegrationFeedId");
                CREATE INDEX "IX_AlertRoutingRules_IsEnabled_EventType" ON "AlertRoutingRules" ("IsEnabled", "EventType");

                PRAGMA foreign_keys = 1;
                """, suppressTransaction: true);
        }
    }
}
