using System;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AssTrackDbContext))]
    [Migration("20260507120000_AddAssetClassesAndSensorReadings")]
    public partial class AddAssetClassesAndSensorReadings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssetClass",
                table: "Assets",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "property");

            migrationBuilder.AddColumn<string>(
                name: "Criticality",
                table: "Assets",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "normal");

            migrationBuilder.Sql("""
                UPDATE Assets
                SET AssetClass = CASE
                    WHEN lower(coalesce(Category, '')) IN ('vehicle', 'van', 'truck', 'car', 'fleet vehicle') THEN 'vehicle'
                    WHEN lower(coalesce(Category, '')) IN ('person', 'people', 'worker', 'driver') THEN 'person'
                    WHEN lower(coalesce(Category, '')) IN ('pet', 'dog', 'cat') THEN 'pet'
                    WHEN lower(coalesce(Category, '')) IN ('equipment', 'generator', 'tool', 'machinery') THEN 'equipment'
                    WHEN lower(coalesce(Category, '')) IN ('container', 'trailer', 'case', 'cargo') THEN 'container'
                    WHEN lower(coalesce(Category, '')) IN ('depot', 'property', 'building', 'site') THEN 'property'
                    ELSE 'property'
                END
                """);

            migrationBuilder.CreateTable(
                name: "SensorReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IntegrationFeedId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SensorType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    NumericValue = table.Column<double>(type: "REAL", nullable: true),
                    TextValue = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    ObservedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SensorReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SensorReadings_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SensorReadings_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SensorReadings_IntegrationFeeds_IntegrationFeedId",
                        column: x => x.IntegrationFeedId,
                        principalTable: "IntegrationFeeds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_AssetId_ObservedAt",
                table: "SensorReadings",
                columns: new[] { "AssetId", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_DeviceId_ObservedAt",
                table: "SensorReadings",
                columns: new[] { "DeviceId", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_IntegrationFeedId",
                table: "SensorReadings",
                column: "IntegrationFeedId");

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_SensorType",
                table: "SensorReadings",
                column: "SensorType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SensorReadings");
            migrationBuilder.DropColumn(name: "AssetClass", table: "Assets");
            migrationBuilder.DropColumn(name: "Criticality", table: "Assets");
        }
    }
}
