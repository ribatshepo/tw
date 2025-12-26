using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace USP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransitEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create transit_keys table
            migrationBuilder.CreateTable(
                name: "transit_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    latest_version = table.Column<int>(type: "integer", nullable: false),
                    min_decryption_version = table.Column<int>(type: "integer", nullable: false),
                    min_encryption_version = table.Column<int>(type: "integer", nullable: false),
                    deletion_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    exportable = table.Column<bool>(type: "boolean", nullable: false),
                    allow_plaintext_backup = table.Column<bool>(type: "boolean", nullable: false),
                    convergent_encryption = table.Column<bool>(type: "boolean", nullable: false),
                    convergent_version = table.Column<int>(type: "integer", nullable: true),
                    derived = table.Column<bool>(type: "boolean", nullable: false),
                    encryption_count = table.Column<long>(type: "bigint", nullable: false),
                    decryption_count = table.Column<long>(type: "bigint", nullable: false),
                    signing_count = table.Column<long>(type: "bigint", nullable: false),
                    verification_count = table.Column<long>(type: "bigint", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transit_keys", x => x.id);
                    table.ForeignKey(
                        name: "fk_transit_keys_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Create transit_key_versions table
            migrationBuilder.CreateTable(
                name: "transit_key_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transit_key_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    encrypted_key_material = table.Column<string>(type: "text", nullable: false),
                    public_key = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    destroyed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transit_key_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_transit_key_versions_transit_keys_transit_key_id",
                        column: x => x.transit_key_id,
                        principalTable: "transit_keys",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_transit_key_versions_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Create indexes for transit_keys
            migrationBuilder.CreateIndex(
                name: "idx_transit_keys_name",
                table: "transit_keys",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_transit_keys_created_by",
                table: "transit_keys",
                column: "created_by");

            // Create indexes for transit_key_versions
            migrationBuilder.CreateIndex(
                name: "idx_transit_key_versions_transit_key_id",
                table: "transit_key_versions",
                column: "transit_key_id");

            migrationBuilder.CreateIndex(
                name: "idx_transit_key_versions_transit_key_id_version",
                table: "transit_key_versions",
                columns: new[] { "transit_key_id", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "transit_key_versions");
            migrationBuilder.DropTable(name: "transit_keys");
        }
    }
}
