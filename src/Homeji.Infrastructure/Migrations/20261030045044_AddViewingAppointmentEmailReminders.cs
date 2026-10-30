using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Homeji.Infrastructure.Migrations;
    /// <inheritdoc />
    public partial class AddViewingAppointmentEmailReminders : Migration
    {
        private static readonly string[] ReminderIndexColumns = ["ReminderNextAttemptAt", "ReminderSentAt"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReminderAttempts",
                schema: "homeji",
                table: "viewing_appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ReminderDeliveryId",
                schema: "homeji",
                table: "viewing_appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReminderDueAt",
                schema: "homeji",
                table: "viewing_appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReminderLeaseUntil",
                schema: "homeji",
                table: "viewing_appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderMinutesBefore",
                schema: "homeji",
                table: "viewing_appointments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReminderNextAttemptAt",
                schema: "homeji",
                table: "viewing_appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReminderSentAt",
                schema: "homeji",
                table: "viewing_appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_viewing_appointments_ReminderNextAttemptAt_ReminderSentAt",
                schema: "homeji",
                table: "viewing_appointments",
                columns: ReminderIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_viewing_appointments_ReminderNextAttemptAt_ReminderSentAt",
                schema: "homeji",
                table: "viewing_appointments");

            migrationBuilder.DropColumn(
                name: "ReminderAttempts",
                schema: "homeji",
                table: "viewing_appointments");

            migrationBuilder.DropColumn(
                name: "ReminderDeliveryId",
                schema: "homeji",
                table: "viewing_appointments");

            migrationBuilder.DropColumn(
                name: "ReminderDueAt",
                schema: "homeji",
                table: "viewing_appointments");

            migrationBuilder.DropColumn(
                name: "ReminderLeaseUntil",
                schema: "homeji",
                table: "viewing_appointments");

            migrationBuilder.DropColumn(
                name: "ReminderMinutesBefore",
                schema: "homeji",
                table: "viewing_appointments");

            migrationBuilder.DropColumn(
                name: "ReminderNextAttemptAt",
                schema: "homeji",
                table: "viewing_appointments");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                schema: "homeji",
                table: "viewing_appointments");
        }
    }
