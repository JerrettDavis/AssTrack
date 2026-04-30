using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeofenceBreaches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeofenceBreaches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ObservationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GeofenceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeofenceBreaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeofenceBreaches_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GeofenceBreaches_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeofenceBreaches_Geofences_GeofenceId",
                        column: x => x.GeofenceId,
                        principalTable: "Geofences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GeofenceBreaches_Observations_ObservationId",
                        column: x => x.ObservationId,
                        principalTable: "Observations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeofenceBreaches_AssetId",
                table: "GeofenceBreaches",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_GeofenceBreaches_DeviceId_DetectedAt",
                table: "GeofenceBreaches",
                columns: new[] { "DeviceId", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GeofenceBreaches_GeofenceId",
                table: "GeofenceBreaches",
                column: "GeofenceId");

            migrationBuilder.CreateIndex(
                name: "IX_GeofenceBreaches_ObservationId",
                table: "GeofenceBreaches",
                column: "ObservationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeofenceBreaches");
        }
    }
}
