using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace USP.Infrastructure.Migrations
{
    /// <summary>
    /// Migration to add HIPAA compliance entities (UserClearance and BusinessAssociateAgreement)
    /// </summary>
    public partial class AddHipaaComplianceEntities : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_clearances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clearance_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    clearance_level = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    background_check_details = table.Column<string>(type: "text", nullable: true),
                    background_check_provider = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    background_check_completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    training_completed = table.Column<string>(type: "text", nullable: true),
                    last_reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    review_notes = table.Column<string>(type: "text", nullable: true),
                    documentation_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_clearances", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_clearances_AspNetUsers_granted_by",
                        column: x => x.granted_by,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_clearances_AspNetUsers_last_reviewed_by",
                        column: x => x.last_reviewed_by,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_clearances_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "business_associate_agreements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    partner_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    partner_contact_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    partner_contact_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    effective_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expiration_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    document_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    document_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    document_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    last_reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    review_notes = table.Column<string>(type: "text", nullable: true),
                    services_provided = table.Column<string>(type: "text", nullable: true),
                    phi_categories = table.Column<string>(type: "text", nullable: true),
                    requires_annual_review = table.Column<bool>(type: "boolean", nullable: false),
                    notify_days_before_expiration = table.Column<int>(type: "integer", nullable: false),
                    renewal_requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    renewal_completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    compliance_notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_associate_agreements", x => x.id);
                    table.ForeignKey(
                        name: "FK_business_associate_agreements_AspNetUsers_last_reviewed_by",
                        column: x => x.last_reviewed_by,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_user_clearances_user_id",
                table: "user_clearances",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_user_clearances_type",
                table: "user_clearances",
                column: "clearance_type");

            migrationBuilder.CreateIndex(
                name: "idx_user_clearances_status",
                table: "user_clearances",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_user_clearances_expires_at",
                table: "user_clearances",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_user_clearances_user_type_status",
                table: "user_clearances",
                columns: new[] { "user_id", "clearance_type", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_baa_partner_id",
                table: "business_associate_agreements",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "idx_baa_partner_name",
                table: "business_associate_agreements",
                column: "partner_name");

            migrationBuilder.CreateIndex(
                name: "idx_baa_status",
                table: "business_associate_agreements",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_baa_expiration_date",
                table: "business_associate_agreements",
                column: "expiration_date");

            migrationBuilder.CreateIndex(
                name: "idx_baa_status_expiration",
                table: "business_associate_agreements",
                columns: new[] { "status", "expiration_date" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_clearances");

            migrationBuilder.DropTable(
                name: "business_associate_agreements");
        }
    }
}
