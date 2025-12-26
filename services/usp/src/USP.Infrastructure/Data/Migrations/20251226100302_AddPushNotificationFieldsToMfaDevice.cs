using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace USP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPushNotificationFieldsToMfaDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DevicePlatform",
                table: "MfaDevices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PushToken",
                table: "MfaDevices",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DevicePlatform",
                table: "MfaDevices");

            migrationBuilder.DropColumn(
                name: "PushToken",
                table: "MfaDevices");
        }
    }
}
