using dnotes_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace dnotes_backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<ManualPayment> ManualPayments => Set<ManualPayment>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Recipient> Recipients => Set<Recipient>();
    public DbSet<Verifier> Verifiers => Set<Verifier>();
    public DbSet<MessageUnlock> MessageUnlocks => Set<MessageUnlock>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpRecord> OtpRecords => Set<OtpRecord>();
    //public DbSet<PaymentRequest> PaymentRequests => Set<PaymentRequest>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ── USER ──────────────────────────────────────
        mb.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);

            e.HasIndex(u => u.Email).IsUnique();

            e.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(255);

            e.Property(u => u.FirstName)
                .HasMaxLength(100);

            e.Property(u => u.LastName)
                .HasMaxLength(100);

            // ✅ FIXED property names
            e.Property(u => u.PhoneNumber)
                .HasMaxLength(20);

            e.Property(u => u.SecondaryPhoneNumber)
                .HasMaxLength(20);

            e.Property(u => u.State)
                .HasMaxLength(100);

            e.Property(u => u.Mandal)
                .HasMaxLength(100);

            e.Property(u => u.Village)
                .HasMaxLength(100);

            e.Property(u => u.Street)
                .HasMaxLength(255);

            e.Property(u => u.HouseNumber)
                .HasMaxLength(50);

            e.Property(u => u.Pincode)
                .HasMaxLength(10);

            // ✅ FIXED encryption fields
            e.Property(u => u.AadhaarEncrypted)
                .HasMaxLength(500);

            e.Property(u => u.PanEncrypted)
                .HasMaxLength(500);

            // Computed property
            e.Ignore(u => u.FullName);
        });

        // ── MESSAGE ───────────────────────────────────
        mb.Entity<Message>(e =>
        {
            e.HasKey(m => m.Id);

            e.Property(m => m.Title)
                .IsRequired()
                .HasMaxLength(500);

            e.Property(m => m.EncryptedBody)
                .HasColumnType("nvarchar(max)");

            e.Property(m => m.DeliveryType)
                .HasMaxLength(50);

            e.Property(m => m.EncryptedDeliveryDate)
                .HasMaxLength(500);

            // ✅ FIXED (renamed correctly)
            e.Property(m => m.EncryptedMediaUrl)
                .HasMaxLength(1000);

            // ✅ MessageType instead of ContentType
            e.Property(m => m.MessageType)
                .HasMaxLength(20);

            // Relationships
            e.HasOne(m => m.Sender)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            e.HasIndex(m => m.SenderId);

            e.HasIndex(m => new { m.IsDelivered, m.IsDraft, m.DeliveryType });
        });

        // ── RECIPIENT ─────────────────────────────────
        mb.Entity<Recipient>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Email).IsRequired().HasMaxLength(255);
            e.Property(r => r.Name).HasMaxLength(255);
            e.Property(r => r.Relationship).HasMaxLength(100);
            e.HasOne(r => r.Message)
             .WithMany(m => m.Recipients)
             .HasForeignKey(r => r.MessageId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── VERIFIER ──────────────────────────────────
        mb.Entity<Verifier>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.Email).IsRequired().HasMaxLength(255);
            e.Property(v => v.Name).HasMaxLength(255);
            e.Property(v => v.Phone).HasMaxLength(20);
            e.Property(v => v.Status).HasConversion<string>();
            e.HasOne(v => v.User)
             .WithOne(u => u.Verifier)
             .HasForeignKey<Verifier>(v => v.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── MESSAGE UNLOCK ────────────────────────────
        mb.Entity<MessageUnlock>(e =>
        {
            e.HasKey(mu => mu.Id);
            e.Property(mu => mu.AmountPaid).HasColumnType("decimal(10,2)");
            e.HasOne(mu => mu.Recipient)
             .WithOne(r => r.MessageUnlock)
             .HasForeignKey<MessageUnlock>(mu => mu.RecipientId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── REFRESH TOKEN ─────────────────────────────
        mb.Entity<RefreshToken>(e =>
        {
            e.HasKey(rt => rt.Id);
            e.Property(rt => rt.Token).IsRequired().HasMaxLength(500);
            e.HasOne(rt => rt.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(rt => rt.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(rt => rt.Token);
        });

        // ── OTP ───────────────────────────────────────
        mb.Entity<OtpRecord>(e =>
        {
            e.HasKey(o => o.Id);

            e.Property(o => o.Target)
                .IsRequired()
                .HasMaxLength(255);

            // ✅ FIXED name
            e.Property(o => o.OtpHash)
                .IsRequired()
                .HasMaxLength(255);

            // ✅ FIXED name (Purpose instead of Type)
            e.Property(o => o.Purpose)
                .HasMaxLength(50);

            // Relationship


            e.HasOne(o => o.User)
                .WithMany(u => u.OtpRecords)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            // Index (FIXED)
            e.HasIndex(o => new { o.Target, o.Purpose, o.IsUsed });
        });

        // ── PAYMENT REQUEST ───────────────────────────
        //mb.Entity<PaymentRequest>(e =>
        //{
        //    e.HasKey(p => p.Id);
        //    e.Property(p => p.Amount).HasColumnType("decimal(10,2)");
        //    e.Property(p => p.UpiTransactionId).HasMaxLength(100);
        //    e.Property(p => p.PaymentMethod).HasMaxLength(50);
        //    e.Property(p => p.ScreenshotUrl).HasMaxLength(1000);
        //    e.Property(p => p.Status).HasConversion<string>();
        //    e.HasOne(p => p.Recipient)
        //     .WithOne()
        //     .HasForeignKey<PaymentRequest>(p => p.RecipientId)
        //     .OnDelete(DeleteBehavior.Cascade);
        //});
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<Message>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(ct);
    }
}