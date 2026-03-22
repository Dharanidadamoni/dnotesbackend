
using dnotes_backend.Models;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace dnotes_backend.Services;

public interface IEmailService
{
    Task SendVerifierWelcomeAsync(Verifier verifier, User user);
    Task SendCheckInWarningAsync(User user);
    Task SendDeathNotificationAsync(Recipient recipient, Message message, User sender);
    Task SendVerifierReminderAsync(Verifier verifier, User user);
    Task SendPaymentConfirmationAsync(string recipientEmail, string senderName);
}

public class EmailService : IEmailService
{
    private readonly SendGridClient _client;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly string _frontendUrl;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _logger = logger;
        _fromEmail = config["SendGrid:FromEmail"]!;
        _fromName = config["SendGrid:FromName"]!;
        _frontendUrl = config["AppSettings:FrontendUrl"]!;
        _client = new SendGridClient(config["SendGrid:ApiKey"]);
    }

    // ── VERIFIER WELCOME ──────────────────────────
    public async Task SendVerifierWelcomeAsync(Verifier verifier, User user)
    {
        var subject = $"{user.FullName} has chosen you as their trusted verifier";
        var body = $@"
        <div style='font-family:Georgia,serif;max-width:560px;margin:0 auto;color:#1a1a1a'>
          <div style='background:#0B0B0D;padding:32px;text-align:center;border-radius:12px 12px 0 0'>
            <h1 style='color:#C4A55A;font-weight:300;font-size:28px;margin:0'>DeathNote</h1>
            <p style='color:#8A8478;margin:8px 0 0;font-size:13px'>Final messages, delivered with care</p>
          </div>
          <div style='background:#fff;padding:40px 36px;border:1px solid #e5e5e5;border-top:none'>
            <h2 style='font-weight:400;font-size:22px;margin:0 0 16px'>You have been chosen</h2>
            <p style='color:#555;line-height:1.8;font-size:15px'>
              Dear {verifier.Name},
            </p>
            <p style='color:#555;line-height:1.8;font-size:15px'>
              <strong>{user.FullName}</strong> has added you as their trusted verifier on DeathNote.
              This means they trust you deeply — with something very important.
            </p>
            <div style='background:#f9f6f0;border-left:3px solid #C4A55A;padding:16px 20px;margin:24px 0;border-radius:0 8px 8px 0'>
              <p style='margin:0;color:#555;font-size:14px;line-height:1.7'>
                <strong style='color:#1a1a1a'>Your role:</strong> When {user.FirstName} passes away,
                please visit the link below and confirm the passing.
                This will trigger the delivery of their final messages to their loved ones.
              </p>
            </div>
            <div style='text-align:center;margin:32px 0'>
              <a href='{_frontendUrl}/verifier/confirm/{verifier.Id}'
                 style='background:#C4A55A;color:#0B0B0D;padding:14px 32px;border-radius:100px;
                        text-decoration:none;font-size:14px;font-weight:500;display:inline-block'>
                Save my verifier link
              </a>
            </div>
            <p style='color:#888;font-size:13px;line-height:1.7'>
              Please save this email. You will also receive a reminder every 6 months.
              You do not need to do anything right now — only when the time comes.
            </p>
          </div>
          <div style='background:#f5f5f5;padding:20px;text-align:center;border-radius:0 0 12px 12px'>
            <p style='color:#aaa;font-size:12px;margin:0'>DeathNote · Words that outlive you</p>
          </div>
        </div>";

        await SendAsync(verifier.Email, verifier.Name, subject, body);
    }

    // ── CHECK-IN WARNING ──────────────────────────
    public async Task SendCheckInWarningAsync(User user)
    {
        var subject = "DeathNote — please confirm you are okay";
        var body = $@"
        <div style='font-family:Georgia,serif;max-width:560px;margin:0 auto;color:#1a1a1a'>
          <div style='background:#0B0B0D;padding:32px;text-align:center;border-radius:12px 12px 0 0'>
            <h1 style='color:#C4A55A;font-weight:300;font-size:28px;margin:0'>DeathNote</h1>
          </div>
          <div style='background:#fff;padding:40px 36px;border:1px solid #e5e5e5;border-top:none'>
            <h2 style='font-weight:400;font-size:22px;margin:0 0 16px'>Are you okay, {user.FirstName}?</h2>
            <p style='color:#555;line-height:1.8;font-size:15px'>
              We have not seen you log in for a while.
              If you are well, please click the button below to let us know.
            </p>
            <p style='color:#555;line-height:1.8;font-size:15px'>
              If we do not hear from you in <strong>7 days</strong>,
              your messages will be delivered to your recipients.
            </p>
            <div style='text-align:center;margin:32px 0'>
              <a href='{_frontendUrl}/checkin?userId={user.Id}'
                 style='background:#C4A55A;color:#0B0B0D;padding:14px 32px;border-radius:100px;
                        text-decoration:none;font-size:15px;font-weight:500;display:inline-block'>
                I am okay — reset my timer
              </a>
            </div>
            <p style='color:#aaa;font-size:13px;'>
              Alternatively, simply log in to your DeathNote account.
            </p>
          </div>
        </div>";

        await SendAsync(user.Email, user.FullName, subject, body);
    }

    // ── DEATH NOTIFICATION TO RECIPIENT ───────────
    public async Task SendDeathNotificationAsync(
        Recipient recipient, Message message, User sender)
    {
        var subject = $"A final message from {sender.FullName}";
        var body = $@"
        <div style='font-family:Georgia,serif;max-width:560px;margin:0 auto;color:#1a1a1a'>
          <div style='background:#0B0B0D;padding:32px;text-align:center;border-radius:12px 12px 0 0'>
            <h1 style='color:#C4A55A;font-weight:300;font-size:28px;margin:0'>DeathNote</h1>
            <p style='color:#8A8478;margin:8px 0 0;font-size:13px'>A message has been waiting for you</p>
          </div>
          <div style='background:#fff;padding:40px 36px;border:1px solid #e5e5e5;border-top:none'>
            <h2 style='font-weight:300;font-size:26px;margin:0 0 16px;font-style:italic'>
              {sender.FullName} left you a final message
            </h2>
            <p style='color:#555;line-height:1.8;font-size:15px'>
              Dear {recipient.Name},
            </p>
            <p style='color:#555;line-height:1.8;font-size:15px'>
              {sender.FullName} wrote you a personal message before passing away.
              They wanted to make sure you received their final words.
            </p>
            <div style='background:#f9f6f0;border-radius:12px;padding:20px 24px;margin:24px 0;
                        border:1px solid #e8d5a3;font-style:italic;color:#555;font-size:15px;line-height:1.8'>
              &ldquo;They left a message titled: <strong>{message.Title}</strong>&rdquo;
            </div>
            <div style='text-align:center;margin:32px 0'>
              <a href='{_frontendUrl}/unlock/{recipient.Id}'
                 style='background:#C4A55A;color:#0B0B0D;padding:16px 36px;border-radius:100px;
                        text-decoration:none;font-size:15px;font-weight:500;display:inline-block'>
                Read their final message
              </a>
            </div>
            <p style='color:#aaa;font-size:12px;text-align:center;line-height:1.7'>
              A small fee of ₹399 is required to unlock the message.<br>
              This helps keep DeathNote running for others like you.
            </p>
          </div>
          <div style='background:#f5f5f5;padding:20px;text-align:center;border-radius:0 0 12px 12px'>
            <p style='color:#aaa;font-size:12px;margin:0'>DeathNote · Words that outlive you</p>
          </div>
        </div>";

        await SendAsync(recipient.Email, recipient.Name, subject, body);
    }

    // ── VERIFIER REMINDER (every 6 months) ────────
    public async Task SendVerifierReminderAsync(Verifier verifier, User user)
    {
        var subject = $"Reminder: You are {user.FullName}'s trusted verifier on DeathNote";
        var body = $@"
        <div style='font-family:Georgia,serif;max-width:560px;margin:0 auto'>
          <div style='background:#0B0B0D;padding:32px;text-align:center;border-radius:12px 12px 0 0'>
            <h1 style='color:#C4A55A;font-weight:300;font-size:28px;margin:0'>DeathNote</h1>
          </div>
          <div style='background:#fff;padding:40px 36px;border:1px solid #e5e5e5;border-top:none'>
            <p style='color:#555;line-height:1.8;font-size:15px'>Dear {verifier.Name},</p>
            <p style='color:#555;line-height:1.8;font-size:15px'>
              This is a reminder that you are the trusted verifier for <strong>{user.FullName}</strong> on DeathNote.
              Please keep this email safe.
            </p>
            <div style='text-align:center;margin:28px 0'>
              <a href='{_frontendUrl}/verifier/confirm/{verifier.Id}'
                 style='background:#C4A55A;color:#0B0B0D;padding:12px 28px;border-radius:100px;
                        text-decoration:none;font-size:14px;font-weight:500;display:inline-block'>
                View my verifier link
              </a>
            </div>
          </div>
        </div>";

        await SendAsync(verifier.Email, verifier.Name, subject, body);
    }

    // ── PAYMENT CONFIRMATION ──────────────────────
    public async Task SendPaymentConfirmationAsync(string recipientEmail, string senderName)
    {
        var subject = $"You have unlocked {senderName}'s message";
        var body = $@"
        <div style='font-family:Georgia,serif;max-width:560px;margin:0 auto'>
          <div style='background:#0B0B0D;padding:32px;text-align:center;border-radius:12px 12px 0 0'>
            <h1 style='color:#C4A55A;font-weight:300;font-size:28px;margin:0'>DeathNote</h1>
          </div>
          <div style='background:#fff;padding:40px 36px;border:1px solid #e5e5e5;border-top:none'>
            <h2 style='font-weight:400;color:#4A8C6E'>Message unlocked ✓</h2>
            <p style='color:#555;line-height:1.8'>
              You now have lifetime access to {senderName}'s final message.
              Take your time — there is no rush.
            </p>
          </div>
        </div>";

        await SendAsync(recipientEmail, "", subject, body);
    }

    // ── PRIVATE SEND ──────────────────────────────
    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        try
        {
            var from = new EmailAddress(_fromEmail, _fromName);
            var to = new EmailAddress(toEmail, toName);

            var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlBody);

            var response = await _client.SendEmailAsync(msg);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Body.ReadAsStringAsync();

                _logger.LogError("SendGrid error: {Status} {Body}", response.StatusCode, body);

                // 🔥 THROW ERROR (IMPORTANT)
                throw new Exception($"Email failed: {response.StatusCode} - {body}");
            }

            _logger.LogInformation("Email sent successfully to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);

            // 🔥 RE-THROW so frontend knows
            throw;
        }
    }
}