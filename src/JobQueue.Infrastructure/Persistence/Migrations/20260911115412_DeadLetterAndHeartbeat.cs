using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobQueue.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeadLetterAndHeartbeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredAt",
                table: "Jobs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastHeartbeatAt",
                table: "Jobs",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "LastHeartbeatAt",
                table: "Jobs");
        }
    }
}
