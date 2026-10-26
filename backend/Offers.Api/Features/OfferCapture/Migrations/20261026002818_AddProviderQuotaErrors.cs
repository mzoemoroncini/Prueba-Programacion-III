using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Offers.Api.Features.OfferCapture.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderQuotaErrors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorCode",
                table: "CaptureRuns",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetryAfterUtc",
                table: "CaptureRuns",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorCode",
                table: "CaptureRuns");

            migrationBuilder.DropColumn(
                name: "RetryAfterUtc",
                table: "CaptureRuns");
        }
    }
}
