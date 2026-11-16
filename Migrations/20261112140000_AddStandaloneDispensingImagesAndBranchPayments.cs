using System;
using AuthDemo.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AuthDemo.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261112140000_AddStandaloneDispensingImagesAndBranchPayments")]
public sealed class AddStandaloneDispensingImagesAndBranchPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PharmacyPrescriptionDrafts",
            columns: table => new {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                PharmacyId = table.Column<int>(type: "int", nullable: false),
                BranchId = table.Column<int>(type: "int", nullable: false),
                CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                PublicPatientId = table.Column<int>(type: "int", nullable: true),
                PatientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                PatientPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                PatientAge = table.Column<int>(type: "int", nullable: false),
                PatientGender = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                DoctorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                PrescriptionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                SubmittedPrescriptionId = table.Column<int>(type: "int", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_PharmacyPrescriptionDrafts", x => x.Id));

        migrationBuilder.CreateTable(
            name: "PharmacyPrescriptionDraftLines",
            columns: table => new {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                DraftId = table.Column<int>(type: "int", nullable: false),
                MedicineId = table.Column<int>(type: "int", nullable: false),
                MedicineName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Strength = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                MorningUnits = table.Column<int>(type: "int", nullable: false),
                AfternoonUnits = table.Column<int>(type: "int", nullable: false),
                EveningUnits = table.Column<int>(type: "int", nullable: false),
                DurationDays = table.Column<int>(type: "int", nullable: false),
                Quantity = table.Column<int>(type: "int", nullable: false),
                UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Instructions = table.Column<string>(type: "nvarchar(max)", nullable: false)
            }, constraints: table => {
                table.PrimaryKey("PK_PharmacyPrescriptionDraftLines", x => x.Id);
                table.ForeignKey("FK_PharmacyPrescriptionDraftLines_PharmacyPrescriptionDrafts_DraftId", x => x.DraftId,
                    "PharmacyPrescriptionDrafts", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PharmacyPrescriptionDoseDetails",
            columns: table => new {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                PrescriptionItemId = table.Column<int>(type: "int", nullable: false),
                MorningUnits = table.Column<int>(type: "int", nullable: false),
                AfternoonUnits = table.Column<int>(type: "int", nullable: false),
                EveningUnits = table.Column<int>(type: "int", nullable: false),
                DurationDays = table.Column<int>(type: "int", nullable: false),
                UnitPriceAtPrescription = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
            }, constraints: table => table.PrimaryKey("PK_PharmacyPrescriptionDoseDetails", x => x.Id));

        migrationBuilder.CreateTable(
            name: "PharmacyUploadedEvidences",
            columns: table => new {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                PharmacyId = table.Column<int>(type: "int", nullable: false),
                BranchId = table.Column<int>(type: "int", nullable: false),
                UploadedByUserId = table.Column<int>(type: "int", nullable: false),
                EvidenceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                DraftId = table.Column<int>(type: "int", nullable: true),
                PrescriptionId = table.Column<int>(type: "int", nullable: true),
                BillId = table.Column<int>(type: "int", nullable: true),
                FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_PharmacyUploadedEvidences", x => x.Id));

        migrationBuilder.CreateTable(
            name: "BranchGatewaySettings",
            columns: table => new {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                PharmacyId = table.Column<int>(type: "int", nullable: false),
                BranchId = table.Column<int>(type: "int", nullable: false),
                MerchantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Environment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                KeyId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                EncryptedKeySecret = table.Column<string>(type: "nvarchar(max)", nullable: false),
                EncryptedWebhookSecret = table.Column<string>(type: "nvarchar(max)", nullable: false),
                WebhookRoutingKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                CashEnabled = table.Column<bool>(type: "bit", nullable: false),
                UpiEnabled = table.Column<bool>(type: "bit", nullable: false),
                CardEnabled = table.Column<bool>(type: "bit", nullable: false),
                BankEnabled = table.Column<bool>(type: "bit", nullable: false),
                PartialPaymentsEnabled = table.Column<bool>(type: "bit", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_BranchGatewaySettings", x => x.Id));

        migrationBuilder.CreateTable(
            name: "PharmacyGatewayAttempts",
            columns: table => new {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                PharmacyId = table.Column<int>(type: "int", nullable: false),
                BranchId = table.Column<int>(type: "int", nullable: false),
                BillId = table.Column<int>(type: "int", nullable: false),
                CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                GatewaySettingsId = table.Column<int>(type: "int", nullable: true),
                IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Method = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                RazorpayOrderId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                RazorpayQrId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                RazorpayPaymentId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_PharmacyGatewayAttempts", x => x.Id));

        migrationBuilder.CreateTable(
            name: "PharmacyGatewayWebhookEvents",
            columns: table => new {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                GatewaySettingsId = table.Column<int>(type: "int", nullable: false),
                EventId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_PharmacyGatewayWebhookEvents", x => x.Id));

        migrationBuilder.CreateTable(
            name: "PharmacyBankTransferRequests",
            columns: table => new {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                PharmacyId = table.Column<int>(type: "int", nullable: false),
                BranchId = table.Column<int>(type: "int", nullable: false),
                BillId = table.Column<int>(type: "int", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Utr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                BankName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                SubmittedByUserId = table.Column<int>(type: "int", nullable: false),
                VerifiedByUserId = table.Column<int>(type: "int", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            }, constraints: table => table.PrimaryKey("PK_PharmacyBankTransferRequests", x => x.Id));

        migrationBuilder.CreateIndex("IX_PharmacyPrescriptionDrafts_Scope", "PharmacyPrescriptionDrafts", new[] { "PharmacyId", "BranchId", "CreatedByUserId", "Status" });
        migrationBuilder.CreateIndex("IX_PharmacyPrescriptionDraftLines_DraftId", "PharmacyPrescriptionDraftLines", "DraftId");
        migrationBuilder.CreateIndex("IX_PharmacyPrescriptionDoseDetails_PrescriptionItemId", "PharmacyPrescriptionDoseDetails", "PrescriptionItemId", unique: true);
        migrationBuilder.CreateIndex("IX_PharmacyUploadedEvidences_Draft", "PharmacyUploadedEvidences", new[] { "PharmacyId", "BranchId", "DraftId" });
        migrationBuilder.CreateIndex("IX_PharmacyUploadedEvidences_Bill", "PharmacyUploadedEvidences", new[] { "PharmacyId", "BranchId", "BillId" });
        migrationBuilder.CreateIndex("IX_BranchGatewaySettings_Scope", "BranchGatewaySettings", new[] { "PharmacyId", "BranchId" }, unique: true);
        migrationBuilder.CreateIndex("IX_BranchGatewaySettings_WebhookRoutingKey", "BranchGatewaySettings", "WebhookRoutingKey", unique: true);
        migrationBuilder.CreateIndex("IX_PharmacyGatewayAttempts_Idempotency", "PharmacyGatewayAttempts", new[] { "PharmacyId", "BranchId", "IdempotencyKey" }, unique: true);
        migrationBuilder.CreateIndex("IX_PharmacyGatewayAttempts_Order", "PharmacyGatewayAttempts", "RazorpayOrderId", unique: true, filter: "[RazorpayOrderId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_PharmacyGatewayAttempts_Qr", "PharmacyGatewayAttempts", "RazorpayQrId", unique: true, filter: "[RazorpayQrId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_PharmacyGatewayAttempts_Payment", "PharmacyGatewayAttempts", "RazorpayPaymentId", unique: true, filter: "[RazorpayPaymentId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_PharmacyGatewayWebhookEvents_Dedup", "PharmacyGatewayWebhookEvents", new[] { "GatewaySettingsId", "EventId" }, unique: true);
        migrationBuilder.CreateIndex("IX_PharmacyBankTransferRequests_Utr", "PharmacyBankTransferRequests", new[] { "PharmacyId", "BranchId", "Utr" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally no destructive rollback: existing and newly uploaded records must be preserved.
        throw new NotSupportedException("This additive migration is not automatically reversible. Back up and review before reverting.");
    }
}
