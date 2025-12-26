using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace USP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Leases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SecretId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RenewalCount = table.Column<int>(type: "integer", nullable: false),
                    AutoRenewalEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    MaxRenewals = table.Column<int>(type: "integer", nullable: true),
                    LastRenewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RevocationReason = table.Column<string>(type: "text", nullable: true),
                    LeaseDurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leases_secrets_SecretId",
                        column: x => x.SecretId,
                        principalTable: "secrets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Leases_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeaseRenewalHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RenewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PreviousExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NewExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RenewalCount = table.Column<int>(type: "integer", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    RenewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsAutoRenewal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaseRenewalHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaseRenewalHistories_Leases_LeaseId",
                        column: x => x.LeaseId,
                        principalTable: "Leases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaseRenewalHistories_LeaseId",
                table: "LeaseRenewalHistories",
                column: "LeaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Leases_SecretId",
                table: "Leases",
                column: "SecretId");

            migrationBuilder.CreateIndex(
                name: "IX_Leases_UserId",
                table: "Leases",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaseRenewalHistories");

            migrationBuilder.DropTable(
                name: "Leases");
        }
    }
}
