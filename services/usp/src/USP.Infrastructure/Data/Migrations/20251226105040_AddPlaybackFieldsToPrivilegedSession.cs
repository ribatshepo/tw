using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace USP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybackFieldsToPrivilegedSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Duration",
                table: "privileged_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FrameRate",
                table: "privileged_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordingFormat",
                table: "privileged_sessions",
                type: "text",
                nullable: false,
                defaultValue: "command-log");

            migrationBuilder.AddColumn<string>(
                name: "VideoCodec",
                table: "privileged_sessions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "privileged_sessions");

            migrationBuilder.DropColumn(
                name: "FrameRate",
                table: "privileged_sessions");

            migrationBuilder.DropColumn(
                name: "RecordingFormat",
                table: "privileged_sessions");

            migrationBuilder.DropColumn(
                name: "VideoCodec",
                table: "privileged_sessions");
        }
    }
}
