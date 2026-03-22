
using dnotes_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace dnotes_backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Recipient> Recipients => Set<Recipient>();
    public DbSet<Verifier> Verifiers => Set<Verifier>();
    public DbSet<MessageUnlock> MessageUnlocks => Set<MessageUnlock>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ── USER ──────────────────────────────────────
        mb.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).IsRequired().HasMaxLength(255);
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.FirstName).HasMaxLength(100);
            e.Property(u => u.LastName).HasMaxLength(100);
            e.Property(u => u.ProfileImageUrl).HasMaxLength(500);
            e.Ignore(u => u.FullName); // computed, not stored
        });

        // ── MESSAGE ───────────────────────────────────
        mb.Entity<Message>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Title).IsRequired().HasMaxLength(500);
            e.Property(m => m.EncryptedBody).IsRequired().HasColumnType("nvarchar(max)");
            e.Property(m => m.EncryptedDeliveryDate).HasMaxLength(500);
            e.Property(m => m.DeliveryType).HasMaxLength(50).HasDefaultValue("immediate");
            e.HasOne(m => m.Sender)
             .WithMany(u => u.Messages)
             .HasForeignKey(m => m.SenderId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => m.SenderId);
            e.HasIndex(m => new { m.SenderId, m.IsDelivered });
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
            e.HasIndex(r => r.MessageId);
        });

        // ── VERIFIER ──────────────────────────────────
        mb.Entity<Verifier>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.Email).IsRequired().HasMaxLength(255);
            e.Property(v => v.Name).IsRequired().HasMaxLength(255);
            e.Property(v => v.Phone).HasMaxLength(20);
            e.Property(v => v.CertificateNumber).HasMaxLength(100);
            e.Property(v => v.CertificateUrl).HasMaxLength(500);
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
            e.Property(mu => mu.StripePaymentIntentId).HasMaxLength(255);
            e.Property(mu => mu.StripeSessionId).HasMaxLength(255);
            e.Property(mu => mu.Currency).HasMaxLength(10);
            e.Property(mu => mu.Status).HasMaxLength(50);
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
            e.Property(rt => rt.CreatedByIp).HasMaxLength(50);
            e.Property(rt => rt.RevokedByIp).HasMaxLength(50);
            e.HasOne(rt => rt.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(rt => rt.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(rt => rt.Token);
            e.HasIndex(rt => new { rt.UserId, rt.IsRevoked });
        });
    }

    // Auto-update UpdatedAt on save
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