using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookRetryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                table: "WebhookDeliveryLogs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "WebhookDeliveryLogs",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveryLogs_CorrelationId",
                table: "WebhookDeliveryLogs",
                column: "CorrelationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookDeliveryLogs_CorrelationId",
                table: "WebhookDeliveryLogs");

            migrationBuilder.DropColumn(
                name: "AttemptNumber",
                table: "WebhookDeliveryLogs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "WebhookDeliveryLogs");
        }
    }
}
