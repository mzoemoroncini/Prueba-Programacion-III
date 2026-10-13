using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Offers.Api.Features.OfferPublication.Migrations
{
    /// <inheritdoc />
    public partial class AddExtractionIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaptureSourceId",
                table: "Offers",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractionKey",
                table: "Offers",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalTitle",
                table: "Offers",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Offers_ExtractionKey",
                table: "Offers",
                column: "ExtractionKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Offers_ExtractionKey",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "CaptureSourceId",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "ExtractionKey",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "OriginalTitle",
                table: "Offers");
        }
    }
}
