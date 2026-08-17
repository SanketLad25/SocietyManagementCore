using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Data;

public class SocietyDbContext(DbContextOptions<SocietyDbContext> options, ICurrentUserContext? currentUser = null)
    : DbContext(options)
{
    public DbSet<Society> Societies => Set<Society>();
    public DbSet<Resident> Residents => Set<Resident>();
    public DbSet<UserLogin> UserLogins => Set<UserLogin>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Flat> Flats => Set<Flat>();
    public DbSet<Parking> Parkings => Set<Parking>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CalculationMethod> CalculationMethods => Set<CalculationMethod>();
    public DbSet<ChargeTargetType> ChargeTargetTypes => Set<ChargeTargetType>();
    public DbSet<SocietyChargeTargetType> SocietyChargeTargetTypes => Set<SocietyChargeTargetType>();
    public DbSet<MaintenanceCategory> MaintenanceCategories => Set<MaintenanceCategory>();
    public DbSet<BillingFrequency> BillingFrequencies => Set<BillingFrequency>();
    public DbSet<ParkingType> ParkingTypes => Set<ParkingType>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<FlatGroup> FlatGroups => Set<FlatGroup>();
    public DbSet<FlatGroupMember> FlatGroupMembers => Set<FlatGroupMember>();
    public DbSet<MaintenanceChargeRule> MaintenanceChargeRules => Set<MaintenanceChargeRule>();
    public DbSet<MaintenanceExemption> MaintenanceExemptions => Set<MaintenanceExemption>();
    public DbSet<AmenitySubscription> AmenitySubscriptions => Set<AmenitySubscription>();
    public DbSet<MaintenanceBillingCycle> MaintenanceBillingCycles => Set<MaintenanceBillingCycle>();
    public DbSet<MaintenanceBill> MaintenanceBills => Set<MaintenanceBill>();
    public DbSet<MaintenanceBillLineItem> MaintenanceBillLineItems => Set<MaintenanceBillLineItem>();
    public DbSet<NoticeCategory> NoticeCategories => Set<NoticeCategory>();
    public DbSet<Notice> Notices => Set<Notice>();
    public DbSet<NoticeAttachment> NoticeAttachments => Set<NoticeAttachment>();
    public DbSet<NoticeReadStatus> NoticeReadStatuses => Set<NoticeReadStatus>();
    public DbSet<EventCategory> EventCategories => Set<EventCategory>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();
    public DbSet<EventNotification> EventNotifications => Set<EventNotification>();
    public DbSet<ComplaintCategory> ComplaintCategories => Set<ComplaintCategory>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<ComplaintUpdate> ComplaintUpdates => Set<ComplaintUpdate>();
    public DbSet<ComplaintAttachment> ComplaintAttachments => Set<ComplaintAttachment>();
    public DbSet<ComplaintNotification> ComplaintNotifications => Set<ComplaintNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Society>(entity =>
        {
            entity.ToTable("Society");
            entity.HasKey(e => e.SocietyId);
            entity.Property(e => e.SocietyName).HasMaxLength(100);
            entity.Property(e => e.Address).HasMaxLength(250);
            entity.Property(e => e.City).HasMaxLength(50);
            entity.Property(e => e.State).HasMaxLength(50);
            entity.Property(e => e.PinCode).HasMaxLength(10);
            entity.Property(e => e.RegistrationNo).HasMaxLength(50);
            entity.Property(e => e.BankName).HasMaxLength(100);
            entity.Property(e => e.BankAccountNumber).HasMaxLength(30);
            entity.Property(e => e.BankIfsc).HasMaxLength(20);
            entity.Property(e => e.ChequePayeeName).HasMaxLength(150);
            entity.Property(e => e.ContactNumber).HasMaxLength(20);
            entity.Property(e => e.InterestOnArrearsPercent).HasColumnType("decimal(5, 2)");
        });

        modelBuilder.Entity<Resident>(entity =>
        {
            entity.ToTable("Resident");
            entity.HasKey(e => e.ResidentId);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Mobile).HasMaxLength(15);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.HasOne<Society>()
                .WithMany()
                .HasForeignKey(e => e.SocietyId);
        });

        modelBuilder.Entity<UserLogin>(entity =>
        {
            entity.ToTable("UserLogin");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.Username).HasMaxLength(50);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.HasOne<Resident>()
                .WithMany()
                .HasForeignKey(e => e.ResidentId);
            entity.HasOne<Role>()
                .WithMany()
                .HasForeignKey(e => e.RoleId);
            entity.HasOne<Society>()
                .WithMany()
                .HasForeignKey(e => e.SocietyId);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Role");
            entity.HasKey(e => e.RoleId);
            entity.Property(e => e.RoleName).HasMaxLength(30);
        });

        modelBuilder.Entity<Flat>(entity =>
        {
            entity.ToTable("Flat");
            entity.HasKey(e => e.FlatId);
            entity.Property(e => e.FlatNo).HasMaxLength(20);
            entity.Property(e => e.AreaSqFt).HasColumnType("decimal(10,2)");
            entity.Property(e => e.MaintenanceAmount).HasColumnType("decimal(10,2)");
            entity.HasOne<Society>()
                .WithMany()
                .HasForeignKey(e => e.SocietyId);
        });

        modelBuilder.Entity<Parking>(entity =>
        {
            entity.ToTable("Parking");
            entity.HasKey(e => e.ParkingId);
            entity.Property(e => e.ParkingNo).HasMaxLength(20);
            entity.Property(e => e.VehicleNo).HasMaxLength(20);
            entity.Property(e => e.VehicleType).HasMaxLength(20);
            entity.HasOne<Society>()
                .WithMany()
                .HasForeignKey(e => e.SocietyId);
            entity.HasOne<Flat>()
                .WithMany()
                .HasForeignKey(e => e.FlatId);
            entity.HasOne<ParkingType>()
                .WithMany()
                .HasForeignKey(e => e.ParkingTypeId);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLog");
            entity.HasKey(e => e.AuditLogId);
            entity.Property(e => e.Action).HasMaxLength(100);
            entity.Property(e => e.TableName).HasMaxLength(100);
            entity.HasOne<Society>()
                .WithMany()
                .HasForeignKey(e => e.SocietyId);
            entity.HasOne<UserLogin>()
                .WithMany()
                .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<CalculationMethod>(entity =>
        {
            entity.ToTable("CalculationMethod");
            entity.HasKey(e => e.CalculationMethodId);
            entity.Property(e => e.MethodCode).HasMaxLength(30);
            entity.Property(e => e.MethodName).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(300);
        });

        modelBuilder.Entity<ChargeTargetType>(entity =>
        {
            entity.ToTable("ChargeTargetType");
            entity.HasKey(e => e.ChargeTargetTypeId);
            entity.Property(e => e.Code).HasMaxLength(30);
            entity.Property(e => e.Label).HasMaxLength(100);
        });

        modelBuilder.Entity<SocietyChargeTargetType>(entity =>
        {
            entity.ToTable("SocietyChargeTargetType");
            entity.HasKey(e => e.SocietyChargeTargetTypeId);
            entity.HasOne<Society>()
                .WithMany()
                .HasForeignKey(e => e.SocietyId);
            entity.HasOne<ChargeTargetType>()
                .WithMany()
                .HasForeignKey(e => e.ChargeTargetTypeId);
        });

        modelBuilder.Entity<MaintenanceCategory>(entity =>
        {
            entity.ToTable("MaintenanceCategory");
            entity.HasKey(e => e.CategoryId);
            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.CategoryCode).HasMaxLength(30);
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.HasOne<Society>()
                .WithMany()
                .HasForeignKey(e => e.SocietyId);
        });

        modelBuilder.Entity<BillingFrequency>(entity =>
        {
            entity.ToTable("BillingFrequency");
            entity.HasKey(e => e.BillingFrequencyId);
            entity.Property(e => e.Label).HasMaxLength(50);
            entity.HasOne<Society>()
                .WithMany()
                .HasForeignKey(e => e.SocietyId);
        });

        modelBuilder.Entity<ParkingType>(entity =>
        {
            entity.ToTable("ParkingType");
            entity.HasKey(e => e.ParkingTypeId);
            entity.Property(e => e.TypeName).HasMaxLength(50);
            entity.HasOne<Society>()
                .WithMany()
                .HasForeignKey(e => e.SocietyId);
        });

        modelBuilder.Entity<Amenity>(entity =>
        {
            entity.ToTable("Amenity");
            entity.HasKey(e => e.AmenityId);
            entity.Property(e => e.AmenityName).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.HasOne<Society>()
                .WithMany()
                .HasForeignKey(e => e.SocietyId);
        });

        modelBuilder.Entity<FlatGroup>(entity =>
        {
            entity.ToTable("FlatGroup");
            entity.HasKey(e => e.FlatGroupId);
            entity.Property(e => e.GroupName).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.HasOne<Society>()
                .WithMany()
                .HasForeignKey(e => e.SocietyId);
        });

        modelBuilder.Entity<FlatGroupMember>(entity =>
        {
            entity.ToTable("FlatGroupMember");
            entity.HasKey(e => new { e.FlatGroupId, e.FlatId });
            entity.HasOne<FlatGroup>()
                .WithMany()
                .HasForeignKey(e => e.FlatGroupId);
            entity.HasOne<Flat>()
                .WithMany()
                .HasForeignKey(e => e.FlatId);
            entity.HasOne<Society>()
                .WithMany()
                .HasForeignKey(e => e.SocietyId);
        });

        modelBuilder.Entity<MaintenanceChargeRule>(entity =>
        {
            entity.ToTable("MaintenanceChargeRule");
            entity.HasKey(e => e.RuleId);
            entity.Property(e => e.Rate).HasColumnType("decimal(12,2)");
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
            entity.HasOne<MaintenanceCategory>().WithMany().HasForeignKey(e => e.CategoryId);
            entity.HasOne<CalculationMethod>().WithMany().HasForeignKey(e => e.CalculationMethodId);
            entity.HasOne<ChargeTargetType>().WithMany().HasForeignKey(e => e.ChargeTargetTypeId);
            entity.HasOne<BillingFrequency>().WithMany().HasForeignKey(e => e.BillingFrequencyId);
        });

        modelBuilder.Entity<MaintenanceExemption>(entity =>
        {
            entity.ToTable("MaintenanceExemption");
            entity.HasKey(e => e.ExemptionId);
            entity.Property(e => e.Reason).HasMaxLength(300);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
            entity.HasOne<MaintenanceCategory>().WithMany().HasForeignKey(e => e.CategoryId);
            entity.HasOne<ChargeTargetType>().WithMany().HasForeignKey(e => e.ChargeTargetTypeId);
        });

        modelBuilder.Entity<AmenitySubscription>(entity =>
        {
            entity.ToTable("AmenitySubscription");
            entity.HasKey(e => e.SubscriptionId);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
            entity.HasOne<Amenity>().WithMany().HasForeignKey(e => e.AmenityId);
            entity.HasOne<Flat>().WithMany().HasForeignKey(e => e.FlatId);
        });

        modelBuilder.Entity<MaintenanceBillingCycle>(entity =>
        {
            entity.ToTable("MaintenanceBillingCycle");
            entity.HasKey(e => e.CycleId);
            entity.Property(e => e.CycleLabel).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
        });

        modelBuilder.Entity<MaintenanceBill>(entity =>
        {
            entity.ToTable("MaintenanceBill");
            entity.HasKey(e => e.BillId);
            entity.Property(e => e.Amount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.OutstandingAmount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.PenaltyAmount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
            entity.HasOne<Flat>().WithMany().HasForeignKey(e => e.FlatId);
            entity.HasOne<MaintenanceBillingCycle>().WithMany().HasForeignKey(e => e.CycleId);
        });

        modelBuilder.Entity<MaintenanceBillLineItem>(entity =>
        {
            entity.ToTable("MaintenanceBillLineItem");
            entity.HasKey(e => e.LineItemId);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.Amount).HasColumnType("decimal(10,2)");
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
            entity.HasOne<MaintenanceBill>().WithMany().HasForeignKey(e => e.BillId);
            entity.HasOne<MaintenanceChargeRule>().WithMany().HasForeignKey(e => e.RuleId);
            entity.HasOne<MaintenanceCategory>().WithMany().HasForeignKey(e => e.CategoryId);
        });

        modelBuilder.Entity<NoticeCategory>(entity =>
        {
            entity.ToTable("NoticeCategory");
            entity.HasKey(e => e.NoticeCategoryId);
            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
        });

        modelBuilder.Entity<Notice>(entity =>
        {
            entity.ToTable("Notice");
            entity.HasKey(e => e.NoticeId);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Priority).HasMaxLength(20);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
            entity.HasOne<NoticeCategory>().WithMany().HasForeignKey(e => e.CategoryId);
        });

        modelBuilder.Entity<NoticeAttachment>(entity =>
        {
            entity.ToTable("NoticeAttachment");
            entity.HasKey(e => e.NoticeAttachmentId);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.StoragePath).HasMaxLength(400);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
            entity.HasOne<Notice>().WithMany().HasForeignKey(e => e.NoticeId);
        });

        modelBuilder.Entity<NoticeReadStatus>(entity =>
        {
            entity.ToTable("NoticeReadStatus");
            entity.HasKey(e => e.NoticeReadStatusId);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
            entity.HasOne<Notice>().WithMany().HasForeignKey(e => e.NoticeId);
            entity.HasOne<UserLogin>().WithMany().HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<EventCategory>(entity =>
        {
            entity.ToTable("EventCategory");
            entity.HasKey(e => e.EventCategoryId);
            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Event");
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.EventName).HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Venue).HasMaxLength(150);
            entity.Property(e => e.CoverEmoji).HasMaxLength(10);
            entity.Property(e => e.CoverImagePath).HasMaxLength(400);
            entity.Property(e => e.OrganizerName).HasMaxLength(150);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
            entity.HasOne<EventCategory>().WithMany().HasForeignKey(e => e.EventCategoryId);
        });

        modelBuilder.Entity<EventRegistration>(entity =>
        {
            entity.ToTable("EventRegistration");
            entity.HasKey(e => e.EventRegistrationId);
            entity.Property(e => e.Comments).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.RegistrationCode).HasMaxLength(20);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
            entity.HasOne<Event>().WithMany().HasForeignKey(e => e.EventId);
            entity.HasOne<Resident>().WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne<Flat>().WithMany().HasForeignKey(e => e.FlatId);
        });

        modelBuilder.Entity<EventNotification>(entity =>
        {
            entity.ToTable("EventNotification");
            entity.HasKey(e => e.EventNotificationId);
            entity.Property(e => e.Message).HasMaxLength(200);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
            entity.HasOne<Event>().WithMany().HasForeignKey(e => e.EventId);
            entity.HasOne<UserLogin>().WithMany().HasForeignKey(e => e.RecipientUserId);
        });

        modelBuilder.Entity<ComplaintCategory>(entity =>
        {
            entity.ToTable("ComplaintCategory");
            entity.HasKey(e => e.ComplaintCategoryId);
            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
        });

        modelBuilder.Entity<Complaint>(entity =>
        {
            entity.ToTable("Complaint");
            entity.HasKey(e => e.ComplaintId);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Priority).HasMaxLength(10);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.AssignedToName).HasMaxLength(100);
            entity.Property(e => e.AssignedToContact).HasMaxLength(20);
            entity.Property(e => e.AssignmentNotes).HasMaxLength(300);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
            entity.HasOne<Resident>().WithMany().HasForeignKey(e => e.ResidentId);
            entity.HasOne<Flat>().WithMany().HasForeignKey(e => e.FlatId);
            entity.HasOne<ComplaintCategory>().WithMany().HasForeignKey(e => e.CategoryId);
            entity.HasOne<UserLogin>().WithMany().HasForeignKey(e => e.AssignedBy);
        });

        modelBuilder.Entity<ComplaintUpdate>(entity =>
        {
            entity.ToTable("ComplaintUpdate");
            entity.HasKey(e => e.ComplaintUpdateId);
            entity.Property(e => e.UpdateType).HasMaxLength(20);
            entity.Property(e => e.OldStatus).HasMaxLength(20);
            entity.Property(e => e.NewStatus).HasMaxLength(20);
            entity.Property(e => e.CommentText).HasMaxLength(500);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
            entity.HasOne<Complaint>().WithMany().HasForeignKey(e => e.ComplaintId);
            entity.HasOne<UserLogin>().WithMany().HasForeignKey(e => e.CreatedBy);
        });

        modelBuilder.Entity<ComplaintAttachment>(entity =>
        {
            entity.ToTable("ComplaintAttachment");
            entity.HasKey(e => e.ComplaintAttachmentId);
            entity.Property(e => e.AttachmentKind).HasMaxLength(20);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.StoragePath).HasMaxLength(400);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
            entity.HasOne<Complaint>().WithMany().HasForeignKey(e => e.ComplaintId);
        });

        modelBuilder.Entity<ComplaintNotification>(entity =>
        {
            entity.ToTable("ComplaintNotification");
            entity.HasKey(e => e.ComplaintNotificationId);
            entity.Property(e => e.Message).HasMaxLength(200);
            entity.HasOne<Society>().WithMany().HasForeignKey(e => e.SocietyId);
            entity.HasOne<Complaint>().WithMany().HasForeignKey(e => e.ComplaintId);
            entity.HasOne<UserLogin>().WithMany().HasForeignKey(e => e.RecipientUserId);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedOn = now;
                entry.Entity.CreatedBy ??= currentUser?.UserId;

                if (entry.Entity is ITenantScoped tenantScoped && tenantScoped.SocietyId == 0 && currentUser?.SocietyId is { } societyId)
                {
                    tenantScoped.SocietyId = societyId;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.ModifiedOn = now;
                entry.Entity.ModifiedBy = currentUser?.UserId;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
