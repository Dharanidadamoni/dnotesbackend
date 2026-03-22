
using dnotes_backend.Data;
using dnotes_backend.Models;
using dnotes_backend.Services;
using Microsoft.EntityFrameworkCore;

namespace dnotes_backend.Services;

/// <summary>
/// The heart of the Death Note app.
/// Runs once every 24 hours at midnight.
/// Checks user activity and triggers message delivery when needed.
///
/// Flow:
///   Day 1-23:  User doesn't log in → nothing happens
///   Day 23:    Send "are you okay?" warning email
///   Day 24-30: Grace period — user can still log in to reset
///   Day 30:    No response → trigger all messages → notify recipients
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeathTriggerService started at {Time}", DateTime.UtcNow);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Calculate delay until next midnight UTC
            var now = DateTime.UtcNow;
            var nextMidnight = now.Date.AddDays(1);
            var delay = nextMidnight - now;

            _logger.LogInformation(
                "Next death trigger check at {NextRun}", nextMidnight);

            await Task.Delay(delay, stoppingToken);

            try
            {
                await RunDailyCheckAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during death trigger check");
            }
        }
    }

    private async Task RunDailyCheckAsync()
    {
        _logger.LogInformation("Running daily death check at {Time}", DateTime.UtcNow);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var warningDays = int.Parse(_config["AppSettings:CheckInWarningDays"] ?? "23");
        var graceDays = int.Parse(_config["AppSettings:CheckInGraceDays"] ?? "7");
        var now = DateTime.UtcNow;
        var warnCutoff = now.AddDays(-warningDays);
        var triggerCutoff = now.AddDays(-(warningDays + graceDays));

        // ── STEP 1: Warn users inactive 23 days ──────────
        var usersToWarn = await db.Users
            .Where(u =>
                !u.IsTriggered &&
                !u.IsDeactivated &&
                (u.SleepModeUntil == null || u.SleepModeUntil < now) &&
                u.LastCheckInAt != null &&
                u.LastCheckInAt < warnCutoff &&
                u.LastCheckInAt >= triggerCutoff)
            .ToListAsync();

        _logger.LogInformation("Users to warn: {Count}", usersToWarn.Count);

        foreach (var user in usersToWarn)
        {
            try
            {
                await emailService.SendCheckInWarningAsync(user);
                _logger.LogInformation("Warning sent to {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send warning to {Email}", user.Email);
            }
        }

        // ── STEP 2: Trigger users inactive 30+ days ──────
        var usersToTrigger = await db.Users
            .Include(u => u.Messages.Where(m => !m.IsDelivered && !m.IsDraft))
                .ThenInclude(m => m.Recipients)
            .Where(u =>
                !u.IsTriggered &&
                !u.IsDeactivated &&
                (u.SleepModeUntil == null || u.SleepModeUntil < now) &&
                u.LastCheckInAt != null &&
                u.LastCheckInAt < triggerCutoff)
            .ToListAsync();

        _logger.LogInformation("Users to trigger: {Count}", usersToTrigger.Count);

        foreach (var user in usersToTrigger)
        {
            await TriggerUserMessagesAsync(user, db, emailService);
        }

        // ── STEP 3: Verifier-confirmed deaths (48hr cooling) ──
        await ProcessVerifierTriggersAsync(db, emailService, now);

        // ── STEP 4: Send verifier reminders (every 6 months) ──
        await SendVerifierRemindersAsync(db, emailService, now);

        _logger.LogInformation(
            "Daily check complete. Warned={Warned}, Triggered={Triggered}",
            usersToWarn.Count, usersToTrigger.Count);
    }

    // ── TRIGGER ALL MESSAGES FOR A USER ──────────────────
    private async Task TriggerUserMessagesAsync(
        Models.User user,
        AppDbContext db,
        IEmailService emailService)
    {
        _logger.LogInformation(
            "Triggering messages for user {UserId} ({Email})",
            user.Id, user.Email);

        // Mark user as triggered first
        user.IsTriggered = true;
        await db.SaveChangesAsync();

        var successCount = 0;
        var failCount = 0;

        foreach (var message in user.Messages)
        {
            foreach (var recipient in message.Recipients.Where(r => !r.IsNotified))
            {
                try
                {
                    await emailService.SendDeathNotificationAsync(recipient, message, user);

                    recipient.IsNotified = true;
                    recipient.NotifiedAt = DateTime.UtcNow;
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to notify {Email} for message {MessageId}",
                        recipient.Email, message.Id);
                    failCount++;
                }
            }

            // Mark message as delivered if all recipients notified
            if (message.Recipients.All(r => r.IsNotified))
            {
                message.IsDelivered = true;
                message.DeliveredAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId}: {Success} notified, {Fail} failed",
            user.Id, successCount, failCount);
    }

    // ── PROCESS VERIFIER-CONFIRMED DEATHS ────────────────
    private async Task ProcessVerifierTriggersAsync(
        AppDbContext db,
        IEmailService emailService,
        DateTime now)
    {
        // 48 hours after verifier reports death — trigger messages
        var coolingHours = int.Parse(
            _config["AppSettings:TriggerCoolingHours"] ?? "48");

        var readyToTrigger = await db.Verifiers
            .Include(v => v.User)
                .ThenInclude(u => u.Messages.Where(m => !m.IsDelivered && !m.IsDraft))
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
            _logger.LogInformation(
                "Processing verifier-confirmed death for user {UserId}",
                verifier.UserId);

            verifier.Status = VerifierStatus.Confirmed;
            verifier.User.IsTriggered = true;
            await db.SaveChangesAsync();

            await TriggerUserMessagesAsync(verifier.User, db, emailService);
        }
    }

    // ── SEND VERIFIER REMINDERS EVERY 6 MONTHS ───────────
    private async Task SendVerifierRemindersAsync(
        AppDbContext db,
        IEmailService emailService,
        DateTime now)
    {
        var sixMonthsAgo = now.AddMonths(-6);

        var verifiersToRemind = await db.Verifiers
            .Include(v => v.User)
            .Where(v =>
                v.Status == VerifierStatus.Active &&
                (v.LastReminderSentAt == null ||
                 v.LastReminderSentAt < sixMonthsAgo))
            .ToListAsync();

        foreach (var verifier in verifiersToRemind)
        {
            try
            {
                await emailService.SendVerifierReminderAsync(verifier, verifier.User);
                verifier.LastReminderSentAt = now;
                _logger.LogInformation(
                    "Reminder sent to verifier {Email}", verifier.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send reminder to verifier {Email}", verifier.Email);
            }
        }

        if (verifiersToRemind.Any())
            await db.SaveChangesAsync();
    }
}