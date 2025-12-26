using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace USP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdaptiveAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdaptiveAuthPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    MinRiskScore = table.Column<int>(type: "integer", nullable: false),
                    MaxRiskScore = table.Column<int>(type: "integer", nullable: false),
                    RequiredFactors = table.Column<string>(type: "text", nullable: false),
                    RequiredFactorCount = table.Column<int>(type: "integer", nullable: false),
                    StepUpValidityMinutes = table.Column<int>(type: "integer", nullable: false),
                    TriggerConditions = table.Column<string>(type: "text", nullable: true),
                    ResourcePatterns = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdaptiveAuthPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdaptiveAuthPolicies_users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "StepUpSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionToken = table.Column<string>(type: "text", nullable: false),
                    CompletedFactors = table.Column<string>(type: "text", nullable: false),
                    InitiatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResourcePath = table.Column<string>(type: "text", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StepUpSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StepUpSessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuthenticationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    RiskScore = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<string>(type: "text", nullable: false),
                    FactorsUsed = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyAction = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    DeviceFingerprint = table.Column<string>(type: "text", nullable: true),
                    IsTrustedDevice = table.Column<bool>(type: "boolean", nullable: false),
                    ResourcePath = table.Column<string>(type: "text", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    EventTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthenticationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuthenticationEvents_AdaptiveAuthPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "AdaptiveAuthPolicies",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AuthenticationEvents_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdaptiveAuthPolicies_CreatorId",
                table: "AdaptiveAuthPolicies",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationEvents_PolicyId",
                table: "AuthenticationEvents",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationEvents_UserId",
                table: "AuthenticationEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StepUpSessions_UserId",
                table: "StepUpSessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthenticationEvents");

            migrationBuilder.DropTable(
                name: "StepUpSessions");

            migrationBuilder.DropTable(
                name: "AdaptiveAuthPolicies");
        }
    }
}
