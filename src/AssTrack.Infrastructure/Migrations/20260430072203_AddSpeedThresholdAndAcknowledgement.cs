using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSpeedThresholdAndAcknowledgement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcknowledgedAtUtc",
                table: "SpeedAlerts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgedBy",
                table: "SpeedAlerts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AcknowledgedAtUtc",
                table: "GeofenceBreaches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgedBy",
                table: "GeofenceBreaches",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SpeedThresholdKmh",
                table: "Assets",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcknowledgedAtUtc",
                table: "SpeedAlerts");

            migrationBuilder.DropColumn(
                name: "AcknowledgedBy",
                table: "SpeedAlerts");

            migrationBuilder.DropColumn(
                name: "AcknowledgedAtUtc",
                table: "GeofenceBreaches");

            migrationBuilder.DropColumn(
                name: "AcknowledgedBy",
                table: "GeofenceBreaches");

            migrationBuilder.DropColumn(
                name: "SpeedThresholdKmh",
                table: "Assets");
        }
    }
}
