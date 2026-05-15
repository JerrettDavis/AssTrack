using System;
using AssTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AssTrackDbContext))]
    [Migration("20260509043000_AddIntegrationEventLifecycle")]
    public partial class AddIntegrationEventLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcknowledgedAt",
                table: "IntegrationEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcknowledgedBy",
                table: "IntegrationEvents",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionNote",
                table: "IntegrationEvents",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "IntegrationEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedBy",
                table: "IntegrationEvents",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "IntegrationEvents",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "open");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_Status_OccurredAt",
                table: "IntegrationEvents",
                columns: new[] { "Status", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationEvents_Status_OccurredAt",
                table: "IntegrationEvents");

            migrationBuilder.DropColumn(
                name: "AcknowledgedAt",
                table: "IntegrationEvents");

            migrationBuilder.DropColumn(
                name: "AcknowledgedBy",
                table: "IntegrationEvents");

            migrationBuilder.DropColumn(
                name: "ResolutionNote",
                table: "IntegrationEvents");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "IntegrationEvents");

            migrationBuilder.DropColumn(
                name: "ResolvedBy",
                table: "IntegrationEvents");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "IntegrationEvents");
        }
    }
}
