using AuthDemo.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthDemo.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // =====================================================
    // CORE TABLES
    // =====================================================

    public DbSet<User> Users { get; set; }

    public DbSet<PharmacyTenant> Pharmacies { get; set; }

    public DbSet<UserPermission> UserPermissions { get; set; }

    public DbSet<AuditLog> AuditLogs { get; set; }

    // =====================================================
    // PHARMACY SUPER ADMIN TABLES
    // =====================================================

    public DbSet<PharmacySystemSetting>
        PharmacySystemSettings
    { get; set; }

    public DbSet<PharmacyNotification>
        PharmacyNotifications
    { get; set; }

    public DbSet<PharmacyBranchSetting>
        PharmacyBranchSettings
    { get; set; }

    // =====================================================
    // HOSPITAL / BRANCH
    // =====================================================

public DbSet<Branch> Branches { get; set; }
    public DbSet<PharmacyBranchIdentity> PharmacyBranchIdentities { get; set; }
    public DbSet<PharmacyPharmacistIdentity> PharmacyPharmacistIdentities { get; set; }
    public DbSet<PharmacyMedicineImportIdentity> PharmacyMedicineImportIdentities { get; set; }

    public DbSet<Patient> Patients { get; set; }

    public DbSet<Doctor> Doctors { get; set; }

    public DbSet<Appointment> Appointments { get; set; }

    // =====================================================
    // PRESCRIPTION TABLES
    // =====================================================

    public DbSet<Prescription> Prescriptions { get; set; }

    public DbSet<PrescriptionItem> PrescriptionItems { get; set; }

    // =====================================================
    // MEDICINE AND INVENTORY TABLES
    // =====================================================

    public DbSet<Medicine> Medicines { get; set; }

    public DbSet<Inventory> Inventories { get; set; }

    public DbSet<MedicineBatch> MedicineBatches { get; set; }

    public DbSet<StockTransaction> StockTransactions { get; set; }

    public DbSet<StockAdjustment> StockAdjustments { get; set; }

    public DbSet<StockTransfer> StockTransfers { get; set; }

    public DbSet<PharmacyAlert> PharmacyAlerts { get; set; }

    // =====================================================
    // SUPPLIER AND PURCHASING TABLES
    // =====================================================

    public DbSet<Supplier> Suppliers { get; set; }

    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }

    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }

    public DbSet<GoodsReceipt> GoodsReceipts { get; set; }

    // =====================================================
    // PHARMACY BILLING TABLES
    // =====================================================

    public DbSet<PharmacyBill> PharmacyBills { get; set; }

    public DbSet<PharmacyBillItem> PharmacyBillItems { get; set; }

    public DbSet<PharmacyPayment> PharmacyPayments { get; set; }

    public DbSet<PharmacyReturn> PharmacyReturns { get; set; }

    // Phase 13R: additive patient-return request header/lines; existing PharmacyReturns preserved.
    public DbSet<PharmacyPatientReturnRequest> PharmacyPatientReturnRequests { get; set; }
    public DbSet<PharmacyPatientReturnRequestLine> PharmacyPatientReturnRequestLines { get; set; }

    // =====================================================
    // BULK MEDICINE IMPORT TABLES
    // =====================================================

    public DbSet<MedicineRecall> MedicineRecalls { get; set; }
    public DbSet<PharmacyShift> PharmacyShifts { get; set; }
    public DbSet<SupplierInvoice> SupplierInvoices { get; set; }

    public DbSet<MedicineImport> MedicineImports { get; set; }

    public DbSet<MedicineImportRow> MedicineImportRows { get; set; }

    // Phase 13: additive prescription, evidence and branch-payment tables.
    public DbSet<PharmacyPrescriptionDraft> PharmacyPrescriptionDrafts { get; set; }
    public DbSet<PharmacyPrescriptionDraftLine> PharmacyPrescriptionDraftLines { get; set; }
    public DbSet<PharmacyPrescriptionDoseDetail> PharmacyPrescriptionDoseDetails { get; set; }
    public DbSet<PharmacyUploadedEvidence> PharmacyUploadedEvidences { get; set; }
    public DbSet<BranchGatewaySettings> BranchGatewaySettings { get; set; }
    public DbSet<PharmacyGatewayAttempt> PharmacyGatewayAttempts { get; set; }
    public DbSet<PharmacyGatewayWebhookEvent> PharmacyGatewayWebhookEvents { get; set; }
    public DbSet<PharmacyBankTransferRequest> PharmacyBankTransferRequests { get; set; }

    // =====================================================
    // MODEL CONFIGURATION
    // =====================================================

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PharmacyMedicineImportIdentity>(entity =>
        {
            entity.HasKey(x => new { x.PharmacyId, x.ImportId });
            entity.HasIndex(x => x.ExistingImportId).IsUnique();
            entity.HasOne(x => x.MedicineImport).WithOne()
                .HasForeignKey<PharmacyMedicineImportIdentity>(x => x.ExistingImportId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PharmacyPharmacistIdentity>(entity =>
        {
            entity.HasKey(x => new { x.PharmacyId, x.PharmacistId });
            entity.HasIndex(x => x.ExistingUserId).IsUnique();
            entity.HasOne(x => x.User).WithOne()
                .HasForeignKey<PharmacyPharmacistIdentity>(x => x.ExistingUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PharmacyBranchIdentity>(entity =>
        {
            entity.HasKey(x => new { x.PharmacyId, x.BranchId });
            entity.HasIndex(x => x.ExistingBranchId).IsUnique();
            entity.HasOne(x => x.Branch)
                .WithOne()
                .HasForeignKey<PharmacyBranchIdentity>(x => x.ExistingBranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PharmacyPrescriptionDraft>(e => {
            e.Property(x => x.PatientName).HasMaxLength(200);
            e.Property(x => x.PatientPhone).HasMaxLength(30);
            e.Property(x => x.PatientGender).HasMaxLength(30);
            e.Property(x => x.DoctorName).HasMaxLength(200);
            e.Property(x => x.Status).HasMaxLength(30);
            e.HasIndex(x => new { x.PharmacyId, x.BranchId, x.CreatedByUserId, x.Status });
        });
        modelBuilder.Entity<PharmacyPrescriptionDraftLine>(e => {
            e.HasOne(x => x.Draft).WithMany(x => x.Lines).HasForeignKey(x => x.DraftId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.MedicineName).HasMaxLength(200);
            e.Property(x => x.Strength).HasMaxLength(100);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.Amount).HasPrecision(18, 2);
        });
        modelBuilder.Entity<PharmacyPrescriptionDoseDetail>(e => {
            e.HasIndex(x => x.PrescriptionItemId).IsUnique();
            e.Property(x => x.UnitPriceAtPrescription).HasPrecision(18, 2);
        });
        modelBuilder.Entity<PharmacyUploadedEvidence>(e => {
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(50);
            e.Property(x => x.EvidenceType).HasMaxLength(40);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.HasIndex(x => new { x.PharmacyId, x.BranchId, x.DraftId });
            e.HasIndex(x => new { x.PharmacyId, x.BranchId, x.BillId });
        });
        modelBuilder.Entity<BranchGatewaySettings>(e => {
            e.HasIndex(x => new { x.PharmacyId, x.BranchId }).IsUnique();
            e.HasIndex(x => x.WebhookRoutingKey).IsUnique();
            e.Property(x => x.MerchantName).HasMaxLength(200);
            e.Property(x => x.KeyId).HasMaxLength(120);
            e.Property(x => x.Environment).HasMaxLength(20);
            e.Property(x => x.WebhookRoutingKey).HasMaxLength(40);
        });
        modelBuilder.Entity<PharmacyGatewayAttempt>(e => {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.IdempotencyKey).HasMaxLength(100);
            e.Property(x => x.Method).HasMaxLength(30);
            e.Property(x => x.Status).HasMaxLength(30);
            e.Property(x => x.RazorpayOrderId).HasMaxLength(120);
            e.Property(x => x.RazorpayQrId).HasMaxLength(120);
            e.Property(x => x.RazorpayPaymentId).HasMaxLength(120);
            e.HasIndex(x => new { x.PharmacyId, x.BranchId, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => x.RazorpayOrderId).IsUnique().HasFilter("[RazorpayOrderId] IS NOT NULL");
            e.HasIndex(x => x.RazorpayQrId).IsUnique().HasFilter("[RazorpayQrId] IS NOT NULL");
            e.HasIndex(x => x.RazorpayPaymentId).IsUnique().HasFilter("[RazorpayPaymentId] IS NOT NULL");
        });
        modelBuilder.Entity<PharmacyGatewayWebhookEvent>(e => {
            e.Property(x => x.EventId).HasMaxLength(120);
            e.Property(x => x.EventType).HasMaxLength(100);
            e.HasIndex(x => new { x.GatewaySettingsId, x.EventId }).IsUnique();
        });
        modelBuilder.Entity<PharmacyBankTransferRequest>(e => {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Utr).HasMaxLength(100);
            e.Property(x => x.BankName).HasMaxLength(150);
            e.Property(x => x.Status).HasMaxLength(30);
            e.HasIndex(x => new { x.PharmacyId, x.BranchId, x.Utr }).IsUnique();
        });

        ConfigureAppointments(modelBuilder);

        ConfigurePrescriptions(modelBuilder);

        ConfigureInventory(modelBuilder);

        ConfigurePurchaseOrders(modelBuilder);

        ConfigureBilling(modelBuilder);

        ConfigureMedicineImports(modelBuilder);

        ConfigurePharmacySuperAdmin(modelBuilder);

        ConfigurePharmacyAdminSettings(modelBuilder);

        ConfigurePharmacyTenants(modelBuilder);

        // Standalone PMS branches belong directly to PharmacyTenant.
        ConfigurePharmacyBranches(modelBuilder);

        // Real-time pharmacy operation field configuration.
        // Keep this before ConfigureIndexes so indexed string columns
        // have bounded SQL Server lengths.
        ConfigureRealtimePharmacyOperations(modelBuilder);

        ConfigureIndexes(modelBuilder);

        ConfigureDecimalPrecision(modelBuilder);

        // =================================================
        // USER ACCOUNT STATUS
        // =================================================

        modelBuilder.Entity<User>()
            .Property(x => x.AccountStatus)
            .HasMaxLength(50)
            .HasDefaultValue("Active");

        modelBuilder.Entity<User>()
            .HasIndex(x => new
            {
                x.Role,
                x.AccountStatus
            });
    }

    // =====================================================
    // APPOINTMENT CONFIGURATION
    // =====================================================

    private static void ConfigureAppointments(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Appointment>()
            .HasOne(x => x.Doctor)
            .WithMany()
            .HasForeignKey(x => x.DoctorId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Appointment>()
            .HasOne(x => x.Patient)
            .WithMany(x => x.Appointments)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.NoAction);


        modelBuilder.Entity<Appointment>()
            .HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    // =====================================================
    // PRESCRIPTION CONFIGURATION
    // =====================================================

    private static void ConfigurePrescriptions(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Prescription>()
            .HasOne(x => x.Appointment)
            .WithMany()
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Prescription>()
            .HasOne(x => x.Doctor)
            .WithMany()
            .HasForeignKey(x => x.DoctorId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Prescription>()
            .HasOne(x => x.Patient)
            .WithMany()
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.NoAction);


        modelBuilder.Entity<Prescription>()
            .HasMany(x => x.Medicines)
            .WithOne(x => x.Prescription)
            .HasForeignKey(x => x.PrescriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PrescriptionItem>()
            .HasOne(x => x.Medicine)
            .WithMany(x => x.PrescriptionItems)
            .HasForeignKey(x => x.MedicineId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    // =====================================================
    // INVENTORY CONFIGURATION
    // =====================================================

    private static void ConfigureInventory(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Inventory>()
            .HasOne(x => x.Medicine)
            .WithMany()
            .HasForeignKey(x => x.MedicineId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Inventory>()
            .Property(x => x.RowVersion)
            .IsRowVersion();

        modelBuilder.Entity<MedicineBatch>()
            .Property(x => x.RowVersion)
            .IsRowVersion();

        modelBuilder.Entity<MedicineBatch>()
            .HasOne(x => x.Inventory)
            .WithMany()
            .HasForeignKey(x => x.InventoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    // =====================================================
    // PURCHASE ORDER CONFIGURATION
    // =====================================================

    private static void ConfigurePurchaseOrders(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PurchaseOrder>()
            .HasMany(x => x.Items)
            .WithOne(x => x.PurchaseOrder)
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PurchaseOrder>()
            .HasOne(x => x.Supplier)
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseOrderItem>()
            .HasOne(x => x.Medicine)
            .WithMany()
            .HasForeignKey(x => x.MedicineId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    // =====================================================
    // BILLING CONFIGURATION
    // =====================================================

    private static void ConfigureBilling(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PharmacyBill>()
            .HasOne(x => x.Prescription)
            .WithMany()
            .HasForeignKey(x => x.PrescriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PharmacyBill>()
            .HasMany(x => x.BillItems)
            .WithOne(x => x.PharmacyBill)
            .HasForeignKey(x => x.PharmacyBillId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PharmacyPayment>()
            .HasOne(x => x.PharmacyBill)
            .WithMany()
            .HasForeignKey(x => x.PharmacyBillId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    // =====================================================
    // BULK MEDICINE IMPORT CONFIGURATION
    // =====================================================

    private static void ConfigureMedicineImports(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MedicineImport>()
            .HasMany(x => x.Rows)
            .WithOne(x => x.MedicineImport)
            .HasForeignKey(x => x.MedicineImportId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MedicineImport>()
            .HasIndex(x => new
            {
                x.PharmacyId,
                x.BranchId,
                x.CreatedAt
            });

        modelBuilder.Entity<MedicineImportRow>()
            .HasIndex(x => new
            {
                x.MedicineImportId,
                x.RowNumber
            })
            .IsUnique();

        modelBuilder.Entity<MedicineImport>()
            .Property(x => x.FileName)
            .HasMaxLength(255);

        modelBuilder.Entity<MedicineImport>()
            .Property(x => x.Status)
            .HasMaxLength(50);

        modelBuilder.Entity<MedicineImportRow>()
            .Property(x => x.MedicineCode)
            .HasMaxLength(100);

        modelBuilder.Entity<MedicineImportRow>()
            .Property(x => x.Action)
            .HasMaxLength(50);

        modelBuilder.Entity<MedicineImportRow>()
            .Property(x => x.Status)
            .HasMaxLength(50);
    }

    // =====================================================
    // PHARMACY SUPER ADMIN CONFIGURATION
    // =====================================================

    private static void ConfigurePharmacySuperAdmin(
        ModelBuilder modelBuilder)
    {
        // =================================================
        // SYSTEM SETTINGS
        // =================================================

        modelBuilder.Entity<PharmacySystemSetting>()
            .Property(x => x.AppName)
            .HasMaxLength(150)
            .IsRequired();

        modelBuilder.Entity<PharmacySystemSetting>()
            .Property(x => x.Timezone)
            .HasMaxLength(100)
            .IsRequired();

        modelBuilder.Entity<PharmacySystemSetting>()
            .Property(x => x.Currency)
            .HasMaxLength(20)
            .IsRequired();

        modelBuilder.Entity<PharmacySystemSetting>()
            .Property(x =>
                x.ConfigurationNotes)
            .HasMaxLength(2000);

        modelBuilder.Entity<PharmacySystemSetting>()
            .HasIndex(x => x.UpdatedAt);

        // =================================================
        // PHARMACY NOTIFICATIONS
        // =================================================

        modelBuilder.Entity<PharmacyNotification>()
            .Property(x => x.Title)
            .HasMaxLength(250)
            .IsRequired();

        modelBuilder.Entity<PharmacyNotification>()
            .Property(x => x.Message)
            .HasMaxLength(4000)
            .IsRequired();

        modelBuilder.Entity<PharmacyNotification>()
            .Property(x => x.RecipientType)
            .HasMaxLength(50)
            .IsRequired();

        modelBuilder.Entity<PharmacyNotification>()
            .HasIndex(x => x.CreatedAt);

        modelBuilder.Entity<PharmacyNotification>()
            .HasIndex(x => new
            {
                x.IsRead,
                x.IsSent
            });

        modelBuilder.Entity<PharmacyNotification>()
            .HasIndex(x => new
            {
                x.BranchId,
                x.UserId
            });
    }

    // =====================================================
    // UNIQUE INDEXES
    // =====================================================

    // =====================================================
    // PHARMACY ADMIN SETTINGS
    // =====================================================

    private static void ConfigurePharmacyAdminSettings(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PharmacyBranchSetting>()
            .HasIndex(x => new { x.PharmacyId, x.BranchId })
            .IsUnique();

        modelBuilder.Entity<PharmacyBranchSetting>()
            .Property(x => x.PharmacyName)
            .HasMaxLength(200)
            .IsRequired();

        modelBuilder.Entity<PharmacyBranchSetting>()
            .Property(x => x.Currency)
            .HasMaxLength(20)
            .IsRequired();

        modelBuilder.Entity<PharmacyBranchSetting>()
            .Property(x => x.InvoicePrefix)
            .HasMaxLength(50)
            .IsRequired();

        modelBuilder.Entity<PharmacyBranchSetting>()
            .Property(x => x.PaymentTerms)
            .HasMaxLength(100)
            .IsRequired();

        modelBuilder.Entity<PharmacyBranchSetting>()
            .Property(x => x.BusinessHours)
            .HasMaxLength(100)
            .IsRequired();

        modelBuilder.Entity<PharmacyBranchSetting>()
            .Property(x => x.SalesInvoiceDetails)
            .HasMaxLength(100)
            .IsRequired();

        modelBuilder.Entity<PharmacyBranchSetting>()
            .Property(x => x.TaxGSTRate)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PharmacyBranchSetting>()
            .Property(x => x.MaximumAllowedDiscount)
            .HasPrecision(18, 2);
    }


    private static void ConfigurePharmacyTenants(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PharmacyTenant>()
            .Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        // Standalone pharmacy identity is PharmacyTenant.Id.
        // required or configured.

        // User -> PharmacyTenant
        modelBuilder.Entity<User>()
            .HasOne(x => x.Pharmacy)
            .WithMany()
            .HasForeignKey(x => x.PharmacyId)
            .OnDelete(DeleteBehavior.NoAction);
    }


    // =====================================================
    // PHARMACY / BRANCH CONFIGURATION
    // =====================================================

    private static void ConfigurePharmacyBranches(
        ModelBuilder modelBuilder)
    {
        // -------------------------------------------------
        // STANDALONE PMS
        //
        // Branch.PharmacyId -> PharmacyTenant.Id
        //
        // A standalone Pharmacy Admin is scoped by PharmacyId.
        // or manage standalone pharmacy branches.
        // -------------------------------------------------

        modelBuilder.Entity<Branch>()
            .HasOne(x => x.Pharmacy)
            .WithMany(x => x.Branches)
            .HasForeignKey(x => x.PharmacyId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Branch>()
            .HasIndex(x => x.PharmacyId);

        modelBuilder.Entity<Branch>()
            .HasIndex(x => new { x.PharmacyId, x.IsMainBranch });

        // Branch names should be unique inside one standalone
        // pharmacy. The filtered index does not affect legacy
        modelBuilder.Entity<Branch>()
            .HasIndex(x => new
            {
                x.PharmacyId,
                x.Name
            })
            .IsUnique()
            .HasFilter("[PharmacyId] IS NOT NULL");

        // -------------------------------------------------
        //
        // -------------------------------------------------

    }


    // =====================================================
    // REAL-TIME PHARMACY OPERATIONS CONFIGURATION
    // =====================================================

    private static void ConfigureRealtimePharmacyOperations(
        ModelBuilder modelBuilder)
    {
        // =================================================
        // MEDICINE
        // =================================================

        modelBuilder.Entity<Medicine>()
            .Property(x => x.Barcode)
            .HasMaxLength(100);

        modelBuilder.Entity<Medicine>()
            .Property(x => x.RecallReason)
            .HasMaxLength(500);

        // =================================================
        // MEDICINE BATCH
        // =================================================

        modelBuilder.Entity<MedicineBatch>()
            .Property(x => x.RecallReason)
            .HasMaxLength(500);

        // RowVersion for Inventory and MedicineBatch is already
        // configured in ConfigureInventory().

        // =================================================
        // MEDICINE RECALL
        // =================================================

        modelBuilder.Entity<MedicineRecall>()
            .Property(x => x.Reason)
            .HasMaxLength(500)
            .IsRequired();

        modelBuilder.Entity<MedicineRecall>()
            .Property(x => x.Status)
            .HasMaxLength(30)
            .IsRequired();

        // =================================================
        // PHARMACY SHIFT
        // =================================================

        modelBuilder.Entity<PharmacyShift>()
            .Property(x => x.Status)
            .HasMaxLength(30)
            .IsRequired();

        modelBuilder.Entity<PharmacyShift>()
            .Property(x => x.Notes)
            .HasMaxLength(1000)
            .IsRequired();

        modelBuilder.Entity<PharmacyShift>()
            .Property(x => x.OpeningCash)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PharmacyShift>()
            .Property(x => x.ClosingCash)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PharmacyShift>()
            .Property(x => x.ExpectedCash)
            .HasPrecision(18, 2);

        // =================================================
        // SUPPLIER INVOICE
        // =================================================

        modelBuilder.Entity<SupplierInvoice>()
            .Property(x => x.InvoiceNumber)
            .HasMaxLength(450)
            .IsRequired();

        modelBuilder.Entity<SupplierInvoice>()
            .Property(x => x.Status)
            .HasMaxLength(30)
            .IsRequired();

        modelBuilder.Entity<SupplierInvoice>()
            .Property(x => x.TotalAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<SupplierInvoice>()
            .Property(x => x.PaidAmount)
            .HasPrecision(18, 2);
    }

    private static void ConfigureIndexes(
        ModelBuilder modelBuilder)
    {
        // Read-heavy list endpoints: indexes added by AddPmsReadPerformanceIndexes migration.
        modelBuilder.Entity<Medicine>()
            .HasIndex(x => new { x.PharmacyId, x.BranchId, x.CreatedAt })
            .HasDatabaseName("IX_Medicines_PharmacyId_BranchId_CreatedAt");

        modelBuilder.Entity<PurchaseOrder>()
            .HasIndex(x => new { x.PharmacyId, x.BranchId, x.OrderDate })
            .HasDatabaseName("IX_PurchaseOrders_PharmacyId_BranchId_OrderDate");

        modelBuilder.Entity<StockTransfer>()
            .HasIndex(x => new { x.PharmacyId, x.FromBranchId, x.RequestedAt })
            .HasDatabaseName("IX_StockTransfers_PharmacyId_FromBranchId_RequestedAt");

        modelBuilder.Entity<Inventory>()
            .HasIndex(x => new
            {
                x.PharmacyId,
                x.BranchId,
                x.MedicineId
            })
            .IsUnique();

        modelBuilder.Entity<Medicine>()
            .HasIndex(x => new
            {
                x.PharmacyId,
                x.BranchId,
                x.MedicineCode
            })
            .IsUnique();

        modelBuilder.Entity<UserPermission>()
            .HasIndex(x => new
            {
                x.UserId,
                x.Module
            })
            .IsUnique();

        modelBuilder.Entity<Supplier>()
            .HasIndex(x => new
            {
                x.PharmacyId,
                x.BranchId,
                x.Code
            })
            .IsUnique();

        modelBuilder.Entity<PurchaseOrder>()
            .HasIndex(x => new
            {
                x.PharmacyId,
                x.BranchId,
                x.OrderNumber
            })
            .IsUnique();

        modelBuilder.Entity<Medicine>()
            .HasIndex(x => new { x.PharmacyId, x.BranchId, x.Barcode })
            .HasFilter("[Barcode] IS NOT NULL");

        modelBuilder.Entity<MedicineBatch>()
            .HasIndex(x => new { x.InventoryId, x.ExpiryDate, x.IsQuarantined, x.IsRecalled });

        modelBuilder.Entity<PharmacyShift>()
            .HasIndex(x => new { x.PharmacyId, x.BranchId, x.UserId, x.Status });

        modelBuilder.Entity<SupplierInvoice>()
            .HasIndex(x => new { x.PharmacyId, x.BranchId, x.InvoiceNumber })
            .IsUnique();

        // =================================================
        // INTEGRATION EXTERNAL IDS
        // =================================================


        modelBuilder.Entity<Branch>()
            .HasIndex(x => new
            {
                x.SourceSystem,
                x.ExternalBranchId
            })
            .IsUnique()
            .HasFilter(
                "[ExternalBranchId] IS NOT NULL");

        modelBuilder.Entity<Doctor>()
            .HasIndex(x => new
            {
                x.SourceSystem,
                x.ExternalDoctorId
            })
            .IsUnique()
            .HasFilter(
                "[ExternalDoctorId] IS NOT NULL");

        modelBuilder.Entity<Patient>()
            .HasIndex(x => new { x.PharmacyId, x.PharmacyPatientId })
            .IsUnique()
            .HasFilter("[PharmacyId] IS NOT NULL AND [PharmacyPatientId] IS NOT NULL");

       
        modelBuilder.Entity<Patient>()
            .HasIndex(x => new
            {
                x.SourceSystem,
                x.ExternalPatientId
            })
            .IsUnique()
            .HasFilter(
                "[ExternalPatientId] IS NOT NULL");

        modelBuilder.Entity<Appointment>()
            .HasIndex(x => new
            {
                x.SourceSystem,
                x.ExternalAppointmentId
            })
            .IsUnique()
            .HasFilter(
                "[ExternalAppointmentId] IS NOT NULL");

        modelBuilder.Entity<Prescription>()
            .HasIndex(x => new
            {
                x.SourceSystem,
                x.ExternalPrescriptionId
            })
            .IsUnique()
            .HasFilter(
                "[ExternalPrescriptionId] IS NOT NULL");

        modelBuilder.Entity<Prescription>()
            .HasIndex(x => x.RequestId)
            .IsUnique()
            .HasFilter(
                "[RequestId] IS NOT NULL");
    }

    // =====================================================
    // DECIMAL PRECISION
    // =====================================================

    private static void ConfigureDecimalPrecision(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Appointment>()
            .Property(x =>
                x.ConsultationFee)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Doctor>()
            .Property(x => x.Fees)
            .HasPrecision(18, 2);

        // =================================================
        // MEDICINE
        // =================================================

        modelBuilder.Entity<Medicine>()
            .Property(x =>
                x.PurchasePrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Medicine>()
            .Property(x =>
                x.SellingPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Medicine>()
            .Property(x => x.GST)
            .HasPrecision(5, 2);

        // =================================================
        // MEDICINE BATCH
        // =================================================

        modelBuilder.Entity<MedicineBatch>()
            .Property(x =>
                x.PurchasePrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<MedicineBatch>()
            .Property(x =>
                x.SellingPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<MedicineBatch>()
            .Property(x => x.GST)
            .HasPrecision(5, 2);

        // =================================================
        // PHARMACY BILL
        // =================================================

        modelBuilder.Entity<PharmacyBill>()
            .Property(x => x.SubTotal)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PharmacyBill>()
            .Property(x => x.Discount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PharmacyBill>()
            .Property(x => x.GSTAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PharmacyBill>()
            .Property(x => x.GrandTotal)
            .HasPrecision(18, 2);

        // =================================================
        // PHARMACY BILL ITEM
        // =================================================

        modelBuilder.Entity<PharmacyBillItem>()
            .Property(x => x.Quantity)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PharmacyBillItem>()
            .Property(x => x.Rate)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PharmacyBillItem>()
            .Property(x => x.GST)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PharmacyBillItem>()
            .Property(x => x.Total)
            .HasPrecision(18, 2);

        // =================================================
        // PHARMACY PAYMENT
        // =================================================

        modelBuilder.Entity<PharmacyPayment>()
            .Property(x => x.PaidAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PharmacyPayment>()
            .Property(x => x.RefundedAmount)
            .HasPrecision(18, 2);

        // =================================================
        // PURCHASE ORDER
        // =================================================

        modelBuilder.Entity<PurchaseOrderItem>()
            .Property(x => x.UnitCost)
            .HasPrecision(18, 2);

        // =================================================
        // PHARMACY RETURN
        // =================================================

        modelBuilder.Entity<PharmacyReturn>()
            .Property(x => x.RefundAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PharmacyPatientReturnRequest>(e =>
        {
            e.Property(x => x.PatientName).HasMaxLength(200);
            e.Property(x => x.Reason).HasMaxLength(200);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => new { x.PharmacyId, x.BranchId, x.CreatedAt });
        });
        modelBuilder.Entity<PharmacyPatientReturnRequestLine>(e =>
        {
            e.Property(x => x.OriginalBatchNumber).HasMaxLength(100);
            e.Property(x => x.Condition).HasMaxLength(40);
            e.HasIndex(x => x.PharmacyReturnId).IsUnique();
            e.HasOne(x => x.Request).WithMany(x => x.Lines)
                .HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.PharmacyReturn).WithOne()
                .HasForeignKey<PharmacyPatientReturnRequestLine>(x => x.PharmacyReturnId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
