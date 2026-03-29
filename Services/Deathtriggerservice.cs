
using dnotes_backend.Data;
using dnotes_backend.Models;
using dnotes_backend.Services;
using Microsoft.EntityFrameworkCore;
namespace dnotes_backend.Services;

/// <summary>
/// The core engine of DeathNote.
/// Runs daily at midnight and processes:
///   1. Inactivity reminders (every 10 weeks, neutral tone)
///   2. Inactivity trigger (after 70-day cooling period)
///   3. Specific date delivery (encrypted date, decrypted at runtime)
///   4. Birthday delivery (encrypted birthday, decrypted at runtime)
///   5. Verifier-confirmed death (48-hour cooling period)
/// </summary>
public class DeathTriggerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeathTriggerService> _logger;
    private readonly IConfiguration _config;

    public DeathTriggerService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeathTriggerService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = config;
    }

    //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    //{
    //    _logger.LogInformation("DeathTriggerService started at {Time}", DateTime.UtcNow);

    //    // ✅ Run IMMEDIATELY on startup — don't wait for midnight
    //    try { await RunDailyCheckAsync(); }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error in initial death check on startup");
    //    }

    //    while (!stoppingToken.IsCancellationRequested)
    //    {
    //        // Then run every 24 hours after that
    //        var now = DateTime.UtcNow;
    //        var nextMidnight = now.Date.AddDays(1);
    //        var delay = nextMidnight - now;

    //        _logger.LogInformation(
    //            "Next death trigger check at {NextRun} (in {Hours:F1} hours)",
    //            nextMidnight, delay.TotalHours);

    //        //await Task.Delay(delay, stoppingToken);
    //        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

    //        try { await RunDailyCheckAsync(); }
    //        catch (Exception ex)
    //        {
    //            _logger.LogError(ex, "Error in daily death check");
    //        }
    //    }
    //}
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔥 DeathTriggerService started");

        while (!stoppingToken.IsCancellationRequested)
          

        {
              _logger.LogWarning("🚨🚨🚨 BACKGROUND SERVICE LOOP RUNNING 🚨🚨🚨");
            try
            {
                await RunDailyCheckAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in death trigger check");
            }

            // 🔥 Run every 5 minutes
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
    private async Task RunDailyCheckAsync()

    {
        _logger.LogInformation("🔥 Running delivery check at {Time}", DateTime.UtcNow);
        _logger.LogInformation("Daily check running at {Time}", DateTime.UtcNow);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var encryptionService = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

        var now = DateTime.UtcNow;

        // ── 1. INACTIVITY REMINDERS (every 10 weeks = 70 days) ────────
        await SendInactivityRemindersAsync(db, emailService, now);

        // ── 2. INACTIVITY TRIGGER (70 days no checkin + 7 day grace) ──
        await ProcessInactivityTriggerAsync(db, emailService, now);

        // ── 3. SPECIFIC DATE DELIVERY ─────────────────────────────────
        await ProcessSpecificDateDeliveryAsync(db, emailService, encryptionService, now);

        // ── 4. BIRTHDAY DELIVERY ──────────────────────────────────────
        await ProcessBirthdayDeliveryAsync(db, emailService, encryptionService, now);

        // ── 5. VERIFIER-CONFIRMED DEATHS (after 48hr cooling) ─────────
        await ProcessVerifierTriggersAsync(db, emailService, now);

        // ── 6. VERIFIER REMINDERS (every 6 months) ────────────────────
        await SendVerifierRemindersAsync(db, emailService, now);
    }

    // ════════════════════════════════════════════════
    // 1. INACTIVITY REMINDERS — every 10 weeks
    //    Neutral tone — no mention of death
    // ════════════════════════════════════════════════
    private async Task SendInactivityRemindersAsync(
        AppDbContext db, IEmailService emailService, DateTime now)
    {
        var tenWeeksAgo = now.AddDays(-70);
        var seventySevenDaysAgo = now.AddDays(-77); // 70 + 7 grace

        var usersToRemind = await db.Users
            .Where(u =>
                !u.IsTriggered &&
                !u.IsDeactivated &&
                (u.SleepModeUntil == null || u.SleepModeUntil < now) &&
                u.LastCheckInAt != null &&
                u.LastCheckInAt < tenWeeksAgo &&
                u.LastCheckInAt >= seventySevenDaysAgo &&
                // Only remind if we haven't reminded in last 7 days
                (u.LastReminderSentAt == null ||
                 u.LastReminderSentAt < now.AddDays(-7)))
            .ToListAsync();

        foreach (var user in usersToRemind)
        {
            
            try
            {
                await emailService.SendActivityReminderAsync(user);
                user.LastReminderSentAt = now;
                user.ReminderCount++;
                _logger.LogInformation(
                    "Activity reminder sent to {Email} (reminder #{Count})",
                    user.Email, user.ReminderCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed reminder for {Email}", user.Email);
            }
        }

        if (usersToRemind.Any()) await db.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════
    // 2. INACTIVITY TRIGGER — 77 days without checkin
    // ════════════════════════════════════════════════
    private async Task ProcessInactivityTriggerAsync(
        AppDbContext db, IEmailService emailService, DateTime now)
    {
        var triggerCutoff = now.AddDays(-77); // 70 days + 7 grace

        var usersToTrigger = await db.Users
            .Include(u => u.Messages
                .Where(m => !m.IsDelivered && !m.IsDraft &&
                            m.DeliveryType == DeliveryTypes.Immediate))
                .ThenInclude(m => m.Recipients)
            .Where(u =>
                !u.IsTriggered &&
                !u.IsDeactivated &&
                (u.SleepModeUntil == null || u.SleepModeUntil < now) &&
                u.LastCheckInAt != null &&
                u.LastCheckInAt < triggerCutoff)
            .ToListAsync();

        foreach (var user in usersToTrigger)
        {
            _logger.LogWarning(
                "Inactivity trigger for user {UserId} ({Email})",
                user.Id, user.Email);
            await TriggerMessageDeliveryAsync(user, db, emailService, "inactivity");
        }
    }

    // ════════════════════════════════════════════════
    // 3. SPECIFIC DATE DELIVERY
    //    Decrypts stored date at runtime
    //    Never stored in plain text
    // ════════════════════════════════════════════════
    private async Task ProcessSpecificDateDeliveryAsync(
      AppDbContext db,
      IEmailService emailService,
      IEncryptionService enc,
      DateTime now)
    {
        // ✅ Get IST timezone
        TimeZoneInfo ist;
        try
        {
            ist = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
        }
        catch
        {
            ist = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        }

        var nowIst = TimeZoneInfo.ConvertTimeFromUtc(now, ist);
        var todayIst = nowIst.Date; // ✅ Date only

        _logger.LogInformation(
            "Checking specific dates — today (IST) = {Today}", todayIst);

        var specificMessages = await db.Messages
            .Include(m => m.Sender)
            .Include(m => m.Recipients)
            .Where(m =>
                !m.IsDelivered &&
                !m.IsDraft &&
                m.DeliveryType == DeliveryTypes.Specific &&
                m.EncryptedDeliveryDate != null &&
                !m.Sender.IsDeactivated)
            .ToListAsync();

        _logger.LogInformation(
            "Found {Count} specific-date messages to check", specificMessages.Count);

        foreach (var message in specificMessages)
        {
    //        _logger.LogInformation(
    //"CHECK → deliveryDate: {DeliveryDate}, today: {Today}, result: {Result}",
    //deliveryDate, todayIst, deliveryDate <= todayIst);
            try
            {
                // 🔓 Decrypt (assumed format: yyyy-MM-dd)
                var decryptedDateStr = enc.Decrypt(message.EncryptedDeliveryDate!);

                // ✅ Convert string → DateTime
                var deliveryDate = DateTime.Parse(decryptedDateStr).Date;

                _logger.LogInformation(
                    "Message {Id}: deliveryDate = {DeliveryDate}, today = {Today}, shouldTrigger = {Trigger}",
                    message.Id, deliveryDate, todayIst, deliveryDate <= todayIst);

                // ✅ CORE FIX: <= logic
                if (deliveryDate <= todayIst)
                {
                    _logger.LogInformation(
                        "✅ Delivering message {Id} (Specific Date Trigger)", message.Id);

                    await DeliverMessageAsync(message, db, emailService, "specific_date");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process specific date for message {Id}", message.Id);
            }
        }
    }

    // ════════════════════════════════════════════════
    // 4. BIRTHDAY DELIVERY
    //    Encrypted as "MM-DD" format
    //    Triggers every year on that date
    // ════════════════════════════════════════════════
    private async Task ProcessBirthdayDeliveryAsync(
        AppDbContext db,
        IEmailService emailService,
        IEncryptionService enc,
        DateTime now)
    {
        // Birthday format stored: "MM-DD"
        var todayMD = now.ToString("MM-dd");

        var birthdayMessages = await db.Messages
            .Include(m => m.Sender)
            .Include(m => m.Recipients)
            .Where(m =>
                !m.IsDraft &&
                m.DeliveryType == DeliveryTypes.Birthday &&
                m.EncryptedDeliveryDate != null &&
                !m.Sender.IsDeactivated &&
                // Only deliver if sender is triggered (passed away)
                // Birthday messages deliver each year after death
                m.Sender.IsTriggered)
            .ToListAsync();

        foreach (var message in birthdayMessages)
        {
            try
            {
                var decryptedBirthday = enc.Decrypt(message.EncryptedDeliveryDate!);

                if (decryptedBirthday == todayMD)
                {
                    // Check if already delivered THIS year
                    var alreadyDeliveredThisYear =
                        message.DeliveredAt?.Year == now.Year;

                    if (!alreadyDeliveredThisYear)
                    {
                        _logger.LogInformation(
                            "Birthday trigger for message {MessageId}", message.Id);

                        // For birthday: re-notify (don't mark as permanently delivered)
                        await NotifyRecipientsAsync(
                            message, db, emailService, "birthday");

                        message.DeliveredAt = now;
                        await db.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Birthday delivery error for message {MessageId}", message.Id);
            }
        }
    }

    // ════════════════════════════════════════════════
    // 5. VERIFIER-CONFIRMED DEATH (48-hour cooling)
    // ════════════════════════════════════════════════
    private async Task ProcessVerifierTriggersAsync(
        AppDbContext db, IEmailService emailService, DateTime now)
    {
        var coolingHours = int.Parse(
            _config["AppSettings:TriggerCoolingHours"] ?? "48");

        var readyToTrigger = await db.Verifiers
            .Include(v => v.User)
                .ThenInclude(u => u.Messages
                    .Where(m => !m.IsDelivered && !m.IsDraft))
                    .ThenInclude(m => m.Recipients)
            .Where(v =>
                v.HasReportedDeath &&
                v.Status == VerifierStatus.DeathReported &&
                v.DeathReportedAt != null &&
                v.DeathReportedAt!.Value.AddHours(coolingHours) <= now &&
                !v.User.IsTriggered)
            .ToListAsync();

        foreach (var verifier in readyToTrigger)
        {
            _logger.LogWarning(
                "Verifier-confirmed death trigger for user {UserId}",
                verifier.UserId);

            verifier.Status = VerifierStatus.Confirmed;
            verifier.User.IsTriggered = true;
            await db.SaveChangesAsync();

            await TriggerMessageDeliveryAsync(
                verifier.User, db, emailService, "verifier_confirmed");
        }
    }

    // ════════════════════════════════════════════════
    // 6. VERIFIER REMINDERS — every 6 months
    // ════════════════════════════════════════════════
    private async Task SendVerifierRemindersAsync(
        AppDbContext db, IEmailService emailService, DateTime now)
    {
        var sixMonthsAgo = now.AddMonths(-6);

        var verifiers = await db.Verifiers
            .Include(v => v.User)
            .Where(v =>
                v.Status == VerifierStatus.Active &&
                (v.LastReminderSentAt == null ||
                 v.LastReminderSentAt < sixMonthsAgo))
            .ToListAsync();

        foreach (var verifier in verifiers)
        {
            try
            {
                await emailService.SendVerifierReminderAsync(verifier, verifier.User);
                verifier.LastReminderSentAt = now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Verifier reminder failed for {Email}", verifier.Email);
            }
        }

        if (verifiers.Any()) await db.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════
    // SHARED: Trigger all messages for a user
    // ════════════════════════════════════════════════
    private async Task TriggerMessageDeliveryAsync(
        User user,
        AppDbContext db,
        IEmailService emailService,
        string triggerReason)
    {
        user.IsTriggered = true;
        await db.SaveChangesAsync();

        // Only deliver "immediate" messages on death trigger
        var messagesToDeliver = user.Messages
            .Where(m =>
                !m.IsDelivered &&
                !m.IsDraft &&
                m.DeliveryType == DeliveryTypes.Immediate)
            .ToList();

        foreach (var message in messagesToDeliver)
        {
            await DeliverMessageAsync(message, db, emailService, triggerReason);
        }
    }

    private async Task DeliverMessageAsync(
        Message message,
        AppDbContext db,
        IEmailService emailService,
        string triggerReason)
    {
        await NotifyRecipientsAsync(message, db, emailService, triggerReason);
        message.IsDelivered = true;
        message.DeliveredAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task NotifyRecipientsAsync(
        Message message,
        AppDbContext db,
        IEmailService emailService,
        string triggerReason)
    {
        foreach (var recipient in message.Recipients.Where(r => !r.IsNotified))
        {
            try
            {
                await emailService.SendDeathNotificationAsync(
                    recipient, message, message.Sender);

                recipient.IsNotified = true;
                recipient.NotifiedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                _logger.LogInformation(
                    "Notified {Email} — trigger: {Reason}",
                    recipient.Email, triggerReason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to notify {Email}", recipient.Email);
            }
        }
    }
}