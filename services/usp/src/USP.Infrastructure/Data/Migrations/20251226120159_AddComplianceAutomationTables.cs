using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace USP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceAutomationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "report_id",
                table: "compliance_controls",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ControlType",
                table: "compliance_controls",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "compliance_controls",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FrameworkName",
                table: "compliance_controls",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "compliance_controls",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "compliance_controls",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ComplianceControlVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ControlId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Evidence = table.Column<string>(type: "text", nullable: true),
                    Findings = table.Column<string>(type: "text", nullable: true),
                    Issues = table.Column<string>(type: "text", nullable: true),
                    Recommendations = table.Column<string>(type: "text", nullable: true),
                    VerificationMethod = table.Column<string>(type: "text", nullable: false),
                    VerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    NextVerificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerificationFrequency = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    VerifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceControlVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceControlVerifications_compliance_controls_ControlId",
                        column: x => x.ControlId,
                        principalTable: "compliance_controls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComplianceControlVerifications_users_VerifierId",
                        column: x => x.VerifierId,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ComplianceVerificationSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ControlId = table.Column<Guid>(type: "uuid", nullable: false),
                    Frequency = table.Column<string>(type: "text", nullable: false),
                    CronExpression = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRunStatus = table.Column<string>(type: "text", nullable: true),
                    LastRunDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    NotificationSettings = table.Column<string>(type: "text", nullable: true),
                    AutoRemediationSettings = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceVerificationSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceVerificationSchedules_compliance_controls_Control~",
                        column: x => x.ControlId,
                        principalTable: "compliance_controls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceRemediationTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VerificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ControlId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    RemediationAction = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AssignedTo = table.Column<Guid>(type: "uuid", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletionNotes = table.Column<string>(type: "text", nullable: true),
                    ImpactLevel = table.Column<string>(type: "text", nullable: true),
                    EstimatedEffort = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    AssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceRemediationTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceRemediationTasks_ComplianceControlVerifications_V~",
                        column: x => x.VerificationId,
                        principalTable: "ComplianceControlVerifications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ComplianceRemediationTasks_compliance_controls_ControlId",
                        column: x => x.ControlId,
                        principalTable: "compliance_controls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComplianceRemediationTasks_users_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ComplianceRemediationTasks_users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceControlVerifications_ControlId",
                table: "ComplianceControlVerifications",
                column: "ControlId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceControlVerifications_VerifierId",
                table: "ComplianceControlVerifications",
                column: "VerifierId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceRemediationTasks_AssignedUserId",
                table: "ComplianceRemediationTasks",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceRemediationTasks_CompletedByUserId",
                table: "ComplianceRemediationTasks",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceRemediationTasks_ControlId",
                table: "ComplianceRemediationTasks",
                column: "ControlId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceRemediationTasks_VerificationId",
                table: "ComplianceRemediationTasks",
                column: "VerificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceVerificationSchedules_ControlId",
                table: "ComplianceVerificationSchedules",
                column: "ControlId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplianceRemediationTasks");

            migrationBuilder.DropTable(
                name: "ComplianceVerificationSchedules");

            migrationBuilder.DropTable(
                name: "ComplianceControlVerifications");

            migrationBuilder.DropColumn(
                name: "ControlType",
                table: "compliance_controls");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "compliance_controls");

            migrationBuilder.DropColumn(
                name: "FrameworkName",
                table: "compliance_controls");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "compliance_controls");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "compliance_controls");

            migrationBuilder.AlterColumn<Guid>(
                name: "report_id",
                table: "compliance_controls",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
