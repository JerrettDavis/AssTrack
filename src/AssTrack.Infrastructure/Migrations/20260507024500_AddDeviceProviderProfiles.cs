using System;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AssTrackDbContext))]
    [Migration("20260507024500_AddDeviceProviderProfiles")]
    public partial class AddDeviceProviderProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderHardwareModel",
                table: "Devices",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderLabel",
                table: "Devices",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderLongName",
                table: "Devices",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderProfileJson",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderProfileUpdatedAt",
                table: "Devices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderRole",
                table: "Devices",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderShortName",
                table: "Devices",
                type: "TEXT",
                maxLength: 80,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ProviderHardwareModel", table: "Devices");
            migrationBuilder.DropColumn(name: "ProviderLabel", table: "Devices");
            migrationBuilder.DropColumn(name: "ProviderLongName", table: "Devices");
            migrationBuilder.DropColumn(name: "ProviderProfileJson", table: "Devices");
            migrationBuilder.DropColumn(name: "ProviderProfileUpdatedAt", table: "Devices");
            migrationBuilder.DropColumn(name: "ProviderRole", table: "Devices");
            migrationBuilder.DropColumn(name: "ProviderShortName", table: "Devices");
        }
    }
}
