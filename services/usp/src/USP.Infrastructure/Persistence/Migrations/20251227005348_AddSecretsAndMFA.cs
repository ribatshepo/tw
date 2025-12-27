using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace USP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecretsAndMFA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_mfa_devices_users_UserId",
                table: "mfa_devices");

            migrationBuilder.DropForeignKey(
                name: "FK_secret_versions_secrets_SecretId",
                table: "secret_versions");

            migrationBuilder.DropForeignKey(
                name: "FK_trusted_devices_users_UserId",
                table: "trusted_devices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_trusted_devices",
                table: "trusted_devices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_secret_versions",
                table: "secret_versions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_mfa_devices",
                table: "mfa_devices");

            migrationBuilder.RenameTable(
                name: "trusted_devices",
                newName: "TrustedDevices");

            migrationBuilder.RenameTable(
                name: "secret_versions",
                newName: "SecretVersions");

            migrationBuilder.RenameTable(
                name: "mfa_devices",
                newName: "MFADevices");

            migrationBuilder.RenameIndex(
                name: "IX_trusted_devices_UserId_DeviceFingerprint",
                table: "TrustedDevices",
                newName: "IX_TrustedDevices_UserId_DeviceFingerprint");

            migrationBuilder.RenameIndex(
                name: "IX_trusted_devices_UserId",
                table: "TrustedDevices",
                newName: "IX_TrustedDevices_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_trusted_devices_IsActive",
                table: "TrustedDevices",
                newName: "IX_TrustedDevices_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_trusted_devices_ExpiresAt",
                table: "TrustedDevices",
                newName: "IX_TrustedDevices_ExpiresAt");

            migrationBuilder.RenameIndex(
                name: "IX_trusted_devices_DeviceFingerprint",
                table: "TrustedDevices",
                newName: "IX_TrustedDevices_DeviceFingerprint");

            migrationBuilder.RenameIndex(
                name: "IX_trusted_devices_DeletedAt",
                table: "TrustedDevices",
                newName: "IX_TrustedDevices_DeletedAt");

            migrationBuilder.RenameIndex(
                name: "IX_trusted_devices_CreatedAt",
                table: "TrustedDevices",
                newName: "IX_TrustedDevices_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_secret_versions_SecretId_Version",
                table: "SecretVersions",
                newName: "IX_SecretVersions_SecretId_Version");

            migrationBuilder.RenameIndex(
                name: "IX_secret_versions_SecretId",
                table: "SecretVersions",
                newName: "IX_SecretVersions_SecretId");

            migrationBuilder.RenameIndex(
                name: "IX_secret_versions_IsDestroyed",
                table: "SecretVersions",
                newName: "IX_SecretVersions_IsDestroyed");

            migrationBuilder.RenameIndex(
                name: "IX_secret_versions_IsDeleted",
                table: "SecretVersions",
                newName: "IX_SecretVersions_IsDeleted");

            migrationBuilder.RenameIndex(
                name: "IX_secret_versions_ExpiresAt",
                table: "SecretVersions",
                newName: "IX_SecretVersions_ExpiresAt");

            migrationBuilder.RenameIndex(
                name: "IX_secret_versions_CreatedAt",
                table: "SecretVersions",
                newName: "IX_SecretVersions_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_mfa_devices_UserId_IsPrimary",
                table: "MFADevices",
                newName: "IX_MFADevices_UserId_IsPrimary");

            migrationBuilder.RenameIndex(
                name: "IX_mfa_devices_UserId",
                table: "MFADevices",
                newName: "IX_MFADevices_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_mfa_devices_Method",
                table: "MFADevices",
                newName: "IX_MFADevices_Method");

            migrationBuilder.RenameIndex(
                name: "IX_mfa_devices_DeletedAt",
                table: "MFADevices",
                newName: "IX_MFADevices_DeletedAt");

            migrationBuilder.RenameIndex(
                name: "IX_mfa_devices_CreatedAt",
                table: "MFADevices",
                newName: "IX_MFADevices_CreatedAt");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "secrets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Algorithm",
                table: "encryption_keys",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "TrustedDevices",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "DeviceType",
                table: "TrustedDevices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "TrustedDevices",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "TrustedDevices",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SecretId",
                table: "SecretVersions",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "EncryptionKeyId",
                table: "SecretVersions",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "SecretVersions",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "SecretVersions",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "MFADevices",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "MFADevices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "MFADevices",
                type: "character varying(36)",
                maxLength: 36,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "MFADevices",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrustedDevices",
                table: "TrustedDevices",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SecretVersions",
                table: "SecretVersions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MFADevices",
                table: "MFADevices",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_secrets_UpdatedAt",
                table: "secrets",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TrustedDevices_ApplicationUserId",
                table: "TrustedDevices",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MFADevices_ApplicationUserId",
                table: "MFADevices",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MFADevices_IsPrimary",
                table: "MFADevices",
                column: "IsPrimary");

            migrationBuilder.CreateIndex(
                name: "IX_MFADevices_UserId_Method",
                table: "MFADevices",
                columns: new[] { "UserId", "Method" });

            migrationBuilder.AddForeignKey(
                name: "FK_MFADevices_users_ApplicationUserId",
                table: "MFADevices",
                column: "ApplicationUserId",
                principalTable: "users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MFADevices_users_UserId",
                table: "MFADevices",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SecretVersions_secrets_SecretId",
                table: "SecretVersions",
                column: "SecretId",
                principalTable: "secrets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TrustedDevices_users_ApplicationUserId",
                table: "TrustedDevices",
                column: "ApplicationUserId",
                principalTable: "users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TrustedDevices_users_UserId",
                table: "TrustedDevices",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MFADevices_users_ApplicationUserId",
                table: "MFADevices");

            migrationBuilder.DropForeignKey(
                name: "FK_MFADevices_users_UserId",
                table: "MFADevices");

            migrationBuilder.DropForeignKey(
                name: "FK_SecretVersions_secrets_SecretId",
                table: "SecretVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_TrustedDevices_users_ApplicationUserId",
                table: "TrustedDevices");

            migrationBuilder.DropForeignKey(
                name: "FK_TrustedDevices_users_UserId",
                table: "TrustedDevices");

            migrationBuilder.DropIndex(
                name: "IX_secrets_UpdatedAt",
                table: "secrets");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrustedDevices",
                table: "TrustedDevices");

            migrationBuilder.DropIndex(
                name: "IX_TrustedDevices_ApplicationUserId",
                table: "TrustedDevices");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SecretVersions",
                table: "SecretVersions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MFADevices",
                table: "MFADevices");

            migrationBuilder.DropIndex(
                name: "IX_MFADevices_ApplicationUserId",
                table: "MFADevices");

            migrationBuilder.DropIndex(
                name: "IX_MFADevices_IsPrimary",
                table: "MFADevices");

            migrationBuilder.DropIndex(
                name: "IX_MFADevices_UserId_Method",
                table: "MFADevices");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "TrustedDevices");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "MFADevices");

            migrationBuilder.RenameTable(
                name: "TrustedDevices",
                newName: "trusted_devices");

            migrationBuilder.RenameTable(
                name: "SecretVersions",
                newName: "secret_versions");

            migrationBuilder.RenameTable(
                name: "MFADevices",
                newName: "mfa_devices");

            migrationBuilder.RenameIndex(
                name: "IX_TrustedDevices_UserId_DeviceFingerprint",
                table: "trusted_devices",
                newName: "IX_trusted_devices_UserId_DeviceFingerprint");

            migrationBuilder.RenameIndex(
                name: "IX_TrustedDevices_UserId",
                table: "trusted_devices",
                newName: "IX_trusted_devices_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_TrustedDevices_IsActive",
                table: "trusted_devices",
                newName: "IX_trusted_devices_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_TrustedDevices_ExpiresAt",
                table: "trusted_devices",
                newName: "IX_trusted_devices_ExpiresAt");

            migrationBuilder.RenameIndex(
                name: "IX_TrustedDevices_DeviceFingerprint",
                table: "trusted_devices",
                newName: "IX_trusted_devices_DeviceFingerprint");

            migrationBuilder.RenameIndex(
                name: "IX_TrustedDevices_DeletedAt",
                table: "trusted_devices",
                newName: "IX_trusted_devices_DeletedAt");

            migrationBuilder.RenameIndex(
                name: "IX_TrustedDevices_CreatedAt",
                table: "trusted_devices",
                newName: "IX_trusted_devices_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_SecretVersions_SecretId_Version",
                table: "secret_versions",
                newName: "IX_secret_versions_SecretId_Version");

            migrationBuilder.RenameIndex(
                name: "IX_SecretVersions_SecretId",
                table: "secret_versions",
                newName: "IX_secret_versions_SecretId");

            migrationBuilder.RenameIndex(
                name: "IX_SecretVersions_IsDestroyed",
                table: "secret_versions",
                newName: "IX_secret_versions_IsDestroyed");

            migrationBuilder.RenameIndex(
                name: "IX_SecretVersions_IsDeleted",
                table: "secret_versions",
                newName: "IX_secret_versions_IsDeleted");

            migrationBuilder.RenameIndex(
                name: "IX_SecretVersions_ExpiresAt",
                table: "secret_versions",
                newName: "IX_secret_versions_ExpiresAt");

            migrationBuilder.RenameIndex(
                name: "IX_SecretVersions_CreatedAt",
                table: "secret_versions",
                newName: "IX_secret_versions_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_MFADevices_UserId_IsPrimary",
                table: "mfa_devices",
                newName: "IX_mfa_devices_UserId_IsPrimary");

            migrationBuilder.RenameIndex(
                name: "IX_MFADevices_UserId",
                table: "mfa_devices",
                newName: "IX_mfa_devices_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_MFADevices_Method",
                table: "mfa_devices",
                newName: "IX_mfa_devices_Method");

            migrationBuilder.RenameIndex(
                name: "IX_MFADevices_DeletedAt",
                table: "mfa_devices",
                newName: "IX_mfa_devices_DeletedAt");

            migrationBuilder.RenameIndex(
                name: "IX_MFADevices_CreatedAt",
                table: "mfa_devices",
                newName: "IX_mfa_devices_CreatedAt");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "secrets",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Algorithm",
                table: "encryption_keys",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "trusted_devices",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "DeviceType",
                table: "trusted_devices",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "trusted_devices",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36);

            migrationBuilder.AlterColumn<string>(
                name: "SecretId",
                table: "secret_versions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36);

            migrationBuilder.AlterColumn<string>(
                name: "EncryptionKeyId",
                table: "secret_versions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "secret_versions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "secret_versions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "mfa_devices",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "mfa_devices",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "mfa_devices",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(36)",
                oldMaxLength: 36);

            migrationBuilder.AddPrimaryKey(
                name: "PK_trusted_devices",
                table: "trusted_devices",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_secret_versions",
                table: "secret_versions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_mfa_devices",
                table: "mfa_devices",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_mfa_devices_users_UserId",
                table: "mfa_devices",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_secret_versions_secrets_SecretId",
                table: "secret_versions",
                column: "SecretId",
                principalTable: "secrets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_trusted_devices_users_UserId",
                table: "trusted_devices",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
