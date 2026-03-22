using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dnotes_backend.Migrations
{
    /// <inheritdoc />
    public partial class NewUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StripePaymentId",
                table: "MessageUnlocks");

            migrationBuilder.RenameColumn(
                name: "PaidAt",
                table: "MessageUnlocks",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "DeliveryDate",
                table: "Messages",
                newName: "DeliveredAt");

            migrationBuilder.RenameColumn(
                name: "Body",
                table: "Messages",
                newName: "EncryptedBody");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Verifiers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificateNumber",
                table: "Verifiers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificateUrl",
                table: "Verifiers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeathReportedAt",
                table: "Verifiers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasReportedDeath",
                table: "Verifiers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReminderSentAt",
                table: "Verifiers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Verifiers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeactivated",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCheckInAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileImageUrl",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNotified",
                table: "Recipients",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "NotifiedAt",
                table: "Recipients",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Relationship",
                table: "Recipients",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "MessageUnlocks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "MessageUnlocks",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "MessageUnlocks",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StripePaymentIntentId",
                table: "MessageUnlocks",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StripeSessionId",
                table: "MessageUnlocks",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "DeliveryType",
                table: "Messages",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "immediate",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedDeliveryDate",
                table: "Messages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WordCount",
                table: "Messages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RevokedByIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SenderId_IsDelivered",
                table: "Messages",
                columns: new[] { "SenderId", "IsDelivered" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_IsRevoked",
                table: "RefreshTokens",
                columns: new[] { "UserId", "IsRevoked" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_Messages_SenderId_IsDelivered",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "CertificateNumber",
                table: "Verifiers");

            migrationBuilder.DropColumn(
                name: "CertificateUrl",
                table: "Verifiers");

            migrationBuilder.DropColumn(
                name: "DeathReportedAt",
                table: "Verifiers");

            migrationBuilder.DropColumn(
                name: "HasReportedDeath",
                table: "Verifiers");

            migrationBuilder.DropColumn(
                name: "LastReminderSentAt",
                table: "Verifiers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Verifiers");

            migrationBuilder.DropColumn(
                name: "IsDeactivated",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastCheckInAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileImageUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsNotified",
                table: "Recipients");

            migrationBuilder.DropColumn(
                name: "NotifiedAt",
                table: "Recipients");

            migrationBuilder.DropColumn(
                name: "Relationship",
                table: "Recipients");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "MessageUnlocks");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "MessageUnlocks");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "MessageUnlocks");

            migrationBuilder.DropColumn(
                name: "StripePaymentIntentId",
                table: "MessageUnlocks");

            migrationBuilder.DropColumn(
                name: "StripeSessionId",
                table: "MessageUnlocks");

            migrationBuilder.DropColumn(
                name: "EncryptedDeliveryDate",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "WordCount",
                table: "Messages");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "MessageUnlocks",
                newName: "PaidAt");

            migrationBuilder.RenameColumn(
                name: "EncryptedBody",
                table: "Messages",
                newName: "Body");

            migrationBuilder.RenameColumn(
                name: "DeliveredAt",
                table: "Messages",
                newName: "DeliveryDate");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Verifiers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripePaymentId",
                table: "MessageUnlocks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "DeliveryType",
                table: "Messages",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldDefaultValue: "immediate");
        }
    }
}
