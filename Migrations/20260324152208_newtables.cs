using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dnotes_backend.Migrations
{
    /// <inheritdoc />
    public partial class newtables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OtpRecords_Target_Type_IsVerified",
                table: "OtpRecords");

            migrationBuilder.DropColumn(
                name: "IdDocumentType",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "OtpRecords");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "EncryptedBirthdayInfo",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "MediaEncryptionKey",
                table: "Messages");

            migrationBuilder.RenameColumn(
                name: "SecondaryPhone",
                table: "Users",
                newName: "SecondaryPhoneNumber");

            migrationBuilder.RenameColumn(
                name: "PrimaryPhone",
                table: "Users",
                newName: "PhoneNumber");

            migrationBuilder.RenameColumn(
                name: "IsPhoneVerified",
                table: "Users",
                newName: "PhoneVerified");

            migrationBuilder.RenameColumn(
                name: "IsEmailVerified",
                table: "Users",
                newName: "IdentityVerified");

            migrationBuilder.RenameColumn(
                name: "EncryptedPan",
                table: "Users",
                newName: "PanEncrypted");

            migrationBuilder.RenameColumn(
                name: "EncryptedAadhaar",
                table: "Users",
                newName: "AadhaarEncrypted");

            migrationBuilder.RenameColumn(
                name: "IsVerified",
                table: "OtpRecords",
                newName: "IsUsed");

            migrationBuilder.RenameColumn(
                name: "HashedOtp",
                table: "OtpRecords",
                newName: "OtpHash");

            migrationBuilder.RenameColumn(
                name: "MediaUrl",
                table: "Messages",
                newName: "EncryptedMediaUrl");

            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReminderCount",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "OtpRecords",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "OtpRecords",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SentTo",
                table: "OtpRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaDurationSeconds",
                table: "Messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageType",
                table: "Messages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ManualPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionRef = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScreenshotUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdminNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VerifiedByAdmin = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManualPayments_Recipients_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Recipients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OtpRecords_Target_Purpose_IsUsed",
                table: "OtpRecords",
                columns: new[] { "Target", "Purpose", "IsUsed" });

            migrationBuilder.CreateIndex(
                name: "IX_ManualPayments_RecipientId",
                table: "ManualPayments",
                column: "RecipientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManualPayments");

            migrationBuilder.DropIndex(
                name: "IX_OtpRecords_Target_Purpose_IsUsed",
                table: "OtpRecords");

            migrationBuilder.DropColumn(
                name: "EmailVerified",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ReminderCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "OtpRecords");

            migrationBuilder.DropColumn(
                name: "SentTo",
                table: "OtpRecords");

            migrationBuilder.DropColumn(
                name: "MediaDurationSeconds",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "MessageType",
                table: "Messages");

            migrationBuilder.RenameColumn(
                name: "SecondaryPhoneNumber",
                table: "Users",
                newName: "SecondaryPhone");

            migrationBuilder.RenameColumn(
                name: "PhoneVerified",
                table: "Users",
                newName: "IsPhoneVerified");

            migrationBuilder.RenameColumn(
                name: "PhoneNumber",
                table: "Users",
                newName: "PrimaryPhone");

            migrationBuilder.RenameColumn(
                name: "PanEncrypted",
                table: "Users",
                newName: "EncryptedPan");

            migrationBuilder.RenameColumn(
                name: "IdentityVerified",
                table: "Users",
                newName: "IsEmailVerified");

            migrationBuilder.RenameColumn(
                name: "AadhaarEncrypted",
                table: "Users",
                newName: "EncryptedAadhaar");

            migrationBuilder.RenameColumn(
                name: "OtpHash",
                table: "OtpRecords",
                newName: "HashedOtp");

            migrationBuilder.RenameColumn(
                name: "IsUsed",
                table: "OtpRecords",
                newName: "IsVerified");

            migrationBuilder.RenameColumn(
                name: "EncryptedMediaUrl",
                table: "Messages",
                newName: "MediaUrl");

            migrationBuilder.AddColumn<string>(
                name: "IdDocumentType",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "OtpRecords",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "OtpRecords",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EncryptedBirthdayInfo",
                table: "Messages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaEncryptionKey",
                table: "Messages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OtpRecords_Target_Type_IsVerified",
                table: "OtpRecords",
                columns: new[] { "Target", "Type", "IsVerified" });
        }
    }
}
