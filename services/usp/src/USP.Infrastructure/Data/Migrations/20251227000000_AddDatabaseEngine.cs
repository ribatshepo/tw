using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace USP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "database_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    plugin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    encrypted_connection_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    encrypted_username = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    encrypted_password = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    max_open_connections = table.Column<int>(type: "integer", nullable: false, defaultValue: 4),
                    max_idle_connections = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    max_connection_lifetime_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 3600),
                    additional_config = table.Column<string>(type: "jsonb", nullable: true),
                    configured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    configured_by = table.Column<Guid>(type: "uuid", nullable: false),
                    last_rotated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_database_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "database_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    database_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    creation_statements = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    revocation_statements = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    renew_statements = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    rollback_statements = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    default_ttl_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 3600),
                    max_ttl_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 86400),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_database_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_database_roles_database_configs_database_config_id",
                        column: x => x.database_config_id,
                        principalTable: "database_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "database_leases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lease_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    database_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    database_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    encrypted_password = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    renewal_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_database_leases", x => x.id);
                    table.ForeignKey(
                        name: "fk_database_leases_database_configs_database_config_id",
                        column: x => x.database_config_id,
                        principalTable: "database_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_database_leases_database_roles_database_role_id",
                        column: x => x.database_role_id,
                        principalTable: "database_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_database_configs_configured_at",
                table: "database_configs",
                column: "configured_at");

            migrationBuilder.CreateIndex(
                name: "ix_database_configs_name",
                table: "database_configs",
                column: "name",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_database_configs_plugin",
                table: "database_configs",
                column: "plugin");

            migrationBuilder.CreateIndex(
                name: "ix_database_leases_created_at",
                table: "database_leases",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_database_leases_database_config_id_is_revoked",
                table: "database_leases",
                columns: new[] { "database_config_id", "is_revoked" });

            migrationBuilder.CreateIndex(
                name: "ix_database_leases_database_role_id",
                table: "database_leases",
                column: "database_role_id");

            migrationBuilder.CreateIndex(
                name: "ix_database_leases_expires_at",
                table: "database_leases",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_database_leases_is_revoked",
                table: "database_leases",
                column: "is_revoked");

            migrationBuilder.CreateIndex(
                name: "ix_database_leases_lease_id",
                table: "database_leases",
                column: "lease_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_database_roles_created_at",
                table: "database_roles",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_database_roles_database_config_id_role_name",
                table: "database_roles",
                columns: new[] { "database_config_id", "role_name" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "database_leases");

            migrationBuilder.DropTable(
                name: "database_roles");

            migrationBuilder.DropTable(
                name: "database_configs");
        }
    }
}
