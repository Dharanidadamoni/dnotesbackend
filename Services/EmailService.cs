
using dnotes_backend.Models;
using Razorpay.Api;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace dnotes_backend.Services;

public interface IEmailService
{
    Task SendRegistrationOtpEmailAsync(
   string toEmail, string firstName, string otp);
    Task SendVerifierWelcomeAsync(Verifier verifier, User user);
    Task SendReminderEmailAsync(User user);
    Task SendDeathNotificationAsync(Recipient recipient, Message message, User sender);
    Task SendVerifierReminderAsync(Verifier verifier, User user);
    Task SendPaymentConfirmationAsync(string recipientEmail, string senderName);
    Task SendPaymentReceivedAsync(string recipientEmail, string upiTxnId);
    Task SendOtpEmailAsync(string toEmail, string firstName, string otp, string purpose);
    Task SendActivityReminderAsync(Models.User user);
    Task SendAdminPaymentNotificationAsync(
        Models.ManualPayment payment,
        Models.Recipient recipient,
        Models.User sender);
}

public class EmailService : IEmailService
{
    private readonly SendGridClient _client;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly string _frontendUrl;
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _logger = logger;
        _fromEmail = config["SendGrid:FromEmail"]!;
        _fromName = config["SendGrid:FromName"]!;
        _frontendUrl = config["AppSettings:FrontendUrl"]!;
        _client = new SendGridClient(config["SendGrid:ApiKey"]);
        _config = config;
    }
    public async Task SendRegistrationOtpEmailAsync(
    string toEmail, string firstName, string otp)
    {
        var subject = $"Welcome to DeathNote — verify your email: {otp}";
        var body = $@"
    <div style='font-family:Georgia,serif;max-width:520px;margin:0 auto'>
      <div style='background:#0B0B0D;padding:28px;text-align:center;border-radius:12px 12px 0 0'>
        <h1 style='color:#C4A55A;font-weight:300;font-size:26px;margin:0'>DeathNote</h1>
        <p style='color:#8A8478;font-size:13px;margin:6px 0 0'>Words that outlive you</p>
      </div>
      <div style='background:#fff;padding:40px 36px;border:1px solid #e5e5e5;border-top:none'>
        <h2 style='font-weight:400;font-size:22px;color:#1a1a1a;margin:0 0 12px'>
          Welcome, {firstName}
        </h2>
        <p style='color:#555;font-size:15px;line-height:1.8;margin:0 0 12px'>
          Thank you for joining DeathNote. Use the code below to
          verify your email address and activate your account.
        </p>
        <p style='color:#555;font-size:15px;line-height:1.8;margin:0 0 28px'>
          This code expires in <strong>10 minutes</strong>.
        </p>
 
        <!-- OTP Box -->
        <div style='text-align:center;margin:0 0 32px'>
          <div style='display:inline-block;background:#f9f6f0;
                      border:2px solid #C4A55A;border-radius:14px;
                      padding:22px 52px'>
            <div style='font-family:monospace;font-size:42px;font-weight:700;
                        color:#1a1a1a;letter-spacing:16px'>{otp}</div>
          </div>
        </div>
 
        <p style='color:#888;font-size:13px;line-height:1.7;text-align:center'>
          If you did not create a DeathNote account, you can safely ignore this email.
        </p>
      </div>
      <div style='background:#f5f5f5;padding:16px;text-align:center;border-radius:0 0 12px 12px'>
        <p style='color:#aaa;font-size:12px;margin:0'>DeathNote · Words that outlive you</p>
      </div>
    </div>";

        await SendAsync(toEmail, firstName, subject, body);
    }
    // Add this method to IEmailService and EmailService:
    public async Task SendLoginOtpEmailAsync(
        string toEmail, string firstName, string otp)
    {
        var subject = $"Your DeathNote login code: {otp}";
        var body = $@"
    <div style='font-family:Georgia,serif;max-width:520px;margin:0 auto'>
      <div style='background:#0B0B0D;padding:28px;text-align:center;border-radius:12px 12px 0 0'>
        <h1 style='color:#C4A55A;font-weight:300;font-size:24px;margin:0'>DeathNote</h1>
      </div>
      <div style='background:#fff;padding:36px;border:1px solid #e5e5e5;border-top:none'>
        <p style='color:#555;font-size:15px;line-height:1.7'>Hi {firstName},</p>
        <p style='color:#555;font-size:15px;line-height:1.7'>
          Use the code below to log in to your DeathNote account.
          This code expires in <strong>10 minutes</strong>.
        </p>
        <div style='text-align:center;margin:32px 0'>
          <div style='display:inline-block;background:#f9f6f0;
                      border:2px solid #C4A55A;border-radius:12px;
                      padding:20px 48px'>
            <div style='font-family:monospace;font-size:40px;font-weight:700;
                        color:#1a1a1a;letter-spacing:14px'>{otp}</div>
          </div>
        </div>
        <p style='color:#888;font-size:13px;line-height:1.7;text-align:center'>
          This code can only be used once.<br/>
          If you did not request this, please ignore this email.
        </p>
      </div>
      <div style='background:#f5f5f5;padding:14px;text-align:center;border-radius:0 0 12px 12px'>
        <p style='color:#aaa;font-size:12px;margin:0'>DeathNote · Words that outlive you</p>
      </div>
    </div>";

        await SendAsync(toEmail, firstName, subject, body);
    }
    // OTP EMAIL
    public async Task SendOtpEmailAsync(
        string toEmail, string firstName, string otp, string purpose)
    {
        var purposeLabel = purpose switch
        {
            "email_verify" => "verify your email address",
            "phone_verify" => "verify your phone number",
            "password_reset" => "reset your password",
            _ => "confirm your identity"
        };

        var subject = $"Your DeathNote verification code: {otp}";
        var body = $@"
    <div style='font-family:Georgia,serif;max-width:520px;margin:0 auto'>
      <div style='background:#0B0B0D;padding:28px;text-align:center;border-radius:12px 12px 0 0'>
        <h1 style='color:#C4A55A;font-weight:300;font-size:24px;margin:0'>DeathNote</h1>
      </div>
      <div style='background:#fff;padding:36px;border:1px solid #e5e5e5;border-top:none'>
        <p style='color:#555;font-size:15px;line-height:1.7'>Hi {firstName},</p>
        <p style='color:#555;font-size:15px;line-height:1.7'>
          Use the code below to {purposeLabel}.
          This code expires in <strong>10 minutes</strong>.
        </p>
        <div style='text-align:center;margin:32px 0'>
          <div style='display:inline-block;background:#f9f6f0;border:2px solid #C4A55A;
                      border-radius:12px;padding:20px 40px'>
            <div style='font-family:monospace;font-size:36px;font-weight:700;
                        color:#1a1a1a;letter-spacing:12px'>{otp}</div>
          </div>
        </div>
        <p style='color:#888;font-size:13px;line-height:1.7'>
          If you did not request this code, please ignore this email.
          Your account is safe.
        </p>
      </div>
    </div>";

        await SendAsync(toEmail, firstName, subject, body);

    }

    // ── REMINDER EMAIL (neutral tone — no mention of death) ──
    public async Task SendReminderEmailAsync(User user)
    {
        var subject = "A gentle reminder from DeathNote — we miss you";
        var body = $@"
        <div style='font-family:Georgia,serif;max-width:560px;margin:0 auto'>
          <div style='background:#0B0B0D;padding:28px;text-align:center;border-radius:12px 12px 0 0'>
            <h1 style='color:#C4A55A;font-weight:300;font-size:26px;margin:0'>DeathNote</h1>
          </div>
          <div style='background:#fff;padding:40px 36px;border:1px solid #e5e5e5;border-top:none'>
            <h2 style='font-weight:400;font-size:22px;margin:0 0 16px'>
              Hello {user.FirstName} 👋
            </h2>
            <p style='color:#555;line-height:1.8;font-size:15px'>
              We noticed it has been a while since you last visited DeathNote.
              Your messages are safely stored and waiting — we just want to make sure
              everything is as you would like it.
            </p>
            <p style='color:#555;line-height:1.8;font-size:15px'>
              Would you like to review or update your messages?
              A quick visit is all it takes to keep everything in order.
            </p>
            <div style='text-align:center;margin:32px 0'>
              <a href='{_frontendUrl}/dashboard'
                 style='background:#C4A55A;color:#0B0B0D;padding:14px 32px;border-radius:100px;
                        text-decoration:none;font-size:14px;font-weight:500;display:inline-block'>
                Visit my messages
              </a>
            </div>
            <p style='color:#aaa;font-size:13px;line-height:1.7'>
              If you believe you received this email in error, you can
              <a href='{_frontendUrl}/settings' style='color:#C4A55A'>manage your preferences</a>.
            </p>
          </div>
        </div>";

        await SendAsync(user.Email, user.FullName, subject, body);
    }

    // ── VERIFIER WELCOME ──────────────────────────────
    public async Task SendVerifierWelcomeAsync(Verifier verifier, User user)
    {
        var subject = $"{user.FullName} has chosen you as their trusted verifier on DeathNote";
        var body = $@"
        <div style='font-family:Georgia,serif;max-width:560px;margin:0 auto'>
          <div style='background:#0B0B0D;padding:28px;text-align:center;border-radius:12px 12px 0 0'>
            <h1 style='color:#C4A55A;font-weight:300;font-size:26px;margin:0'>DeathNote</h1>
          </div>
          <div style='background:#fff;padding:40px 36px;border:1px solid #e5e5e5;border-top:none'>
            <h2 style='font-weight:400;font-size:22px;margin:0 0 16px'>
              Dear {verifier.Name},
            </h2>
            <p style='color:#555;line-height:1.8;font-size:15px'>
              <strong>{user.FullName}</strong> has added you as their trusted verifier on DeathNote.
              When the time comes, your role will be to visit the link below and confirm.
            </p>
            <div style='background:#f9f6f0;border-left:3px solid #C4A55A;padding:16px 20px;margin:24px 0'>
              <p style='margin:0;color:#555;font-size:14px;line-height:1.7'>
                This is an important responsibility. Please save this email and
                keep the link below accessible.
              </p>
            </div>
            <div style='text-align:center;margin:32px 0'>
              <a href='{_frontendUrl}/verifier/confirm/{verifier.Id}'
                 style='background:#C4A55A;color:#0B0B0D;padding:14px 32px;border-radius:100px;
                        text-decoration:none;font-size:14px;font-weight:500;display:inline-block'>
                Save my verifier link
              </a>
            </div>
          </div>
        </div>";

        await SendAsync(verifier.Email, verifier.Name, subject, body);
    }
    // ACTIVITY REMINDER — NEUTRAL TONE (no death mention)
    public async Task SendActivityReminderAsync(Models.User user)
    {
        var subject = $"A quick hello from DeathNote, {user.FirstName}";
        var body = $@"
    <div style='font-family:Georgia,serif;max-width:520px;margin:0 auto'>
      <div style='background:#0B0B0D;padding:28px;text-align:center;border-radius:12px 12px 0 0'>
        <h1 style='color:#C4A55A;font-weight:300;font-size:24px;margin:0'>DeathNote</h1>
      </div>
      <div style='background:#fff;padding:36px;border:1px solid #e5e5e5;border-top:none'>
        <h2 style='font-weight:400;font-size:22px;color:#1a1a1a;margin:0 0 16px'>
          Hi {user.FirstName}, we miss you
        </h2>
        <p style='color:#555;font-size:15px;line-height:1.8'>
          It has been a while since you visited DeathNote.
          Your messages are safely stored and waiting — we just wanted to make sure
          everything is well on your end.
        </p>
        <p style='color:#555;font-size:15px;line-height:1.8'>
          Simply log in to let us know you are active.
          It takes just a moment and keeps your account in good standing.
        </p>
        <div style='text-align:center;margin:32px 0'>
          <a href='{_frontendUrl}/login'
             style='background:#C4A55A;color:#0B0B0D;padding:14px 36px;
                    border-radius:100px;text-decoration:none;
                    font-size:15px;font-weight:500;display:inline-block'>
            Log in to DeathNote
          </a>
        </div>
        <p style='color:#aaa;font-size:13px;line-height:1.7'>
          If you are going through a busy period or travelling,
          you can enable Sleep Mode from your settings to pause reminders.
        </p>
      </div>
      <div style='background:#f5f5f5;padding:16px;text-align:center;border-radius:0 0 12px 12px'>
        <p style='color:#aaa;font-size:12px;margin:0'>DeathNote · Words that outlive you</p>
      </div>
    </div>";

        await SendAsync(user.Email, user.FullName, subject, body);
    }
    // ADMIN PAYMENT NOTIFICATION
public async Task SendAdminPaymentNotificationAsync(
    Models.ManualPayment payment,
    Models.Recipient recipient,
    Models.User sender)
    {
        var adminEmail = _config["Payment:AdminEmail"] ?? "admin@deathnote.app";
        var subject = $"[Action Required] Payment to verify — {payment.TransactionRef}";
        var body = $@"
    <div style='font-family:sans-serif;max-width:560px;margin:0 auto'>
      <h2 style='color:#1a1a1a'>New payment to verify</h2>
      <table style='width:100%;border-collapse:collapse'>
        <tr><td style='padding:8px;border:1px solid #e5e5e5;background:#f9f9f9;font-weight:600'>Recipient</td>
            <td style='padding:8px;border:1px solid #e5e5e5'>{recipient.Name} ({recipient.Email})</td></tr>
        <tr><td style='padding:8px;border:1px solid #e5e5e5;background:#f9f9f9;font-weight:600'>Sender</td>
            <td style='padding:8px;border:1px solid #e5e5e5'>{sender.FullName}</td></tr>
        <tr><td style='padding:8px;border:1px solid #e5e5e5;background:#f9f9f9;font-weight:600'>Method</td>
            <td style='padding:8px;border:1px solid #e5e5e5'>{payment.PaymentMethod.ToUpper()}</td></tr>
        <tr><td style='padding:8px;border:1px solid #e5e5e5;background:#f9f9f9;font-weight:600'>Transaction Ref</td>
            <td style='padding:8px;border:1px solid #e5e5e5'><strong>{payment.TransactionRef}</strong></td></tr>
        <tr><td style='padding:8px;border:1px solid #e5e5e5;background:#f9f9f9;font-weight:600'>Amount</td>
            <td style='padding:8px;border:1px solid #e5e5e5'>₹{payment.AmountPaid}</td></tr>
      </table>
      <div style='margin-top:20px'>
        <a href='{_frontendUrl}/admin/payments/{payment.Id}'
           style='background:#C4A55A;color:#0B0B0D;padding:12px 28px;
                  border-radius:8px;text-decoration:none;font-weight:600'>
          Verify Payment →
        </a>
      </div>
    </div>";

        await SendAsync(adminEmail, "Admin", subject, body);
    }

    // ── DEATH NOTIFICATION ────────────────────────────
    // ════════════════════════════════════════════════
    // REPLACE SendDeathNotificationAsync in EmailService.cs
    // This adds the payment link directly into the email
    // ════════════════════════════════════════════════

    public async Task SendDeathNotificationAsync(
        Recipient recipient, Message message, User sender)
    {
        // Payment + unlock link — same URL, page handles both states
        var unlockUrl = $"{_frontendUrl}/unlock/{recipient.Id}";
        var subject = $"A final message from {sender.FullName}";

        var body = $@"
    <!DOCTYPE html>
    <html>
    <head><meta charset='utf-8'><meta name='viewport' content='width=device-width'></head>
    <body style='margin:0;padding:0;background:#f5f5f5;font-family:Georgia,serif'>
      <div style='max-width:580px;margin:0 auto;padding:24px 16px'>

        <!-- Header -->
        <div style='background:#0B0B0D;padding:32px;text-align:center;border-radius:16px 16px 0 0'>
          <h1 style='color:#C4A55A;font-weight:300;font-size:28px;margin:0;letter-spacing:0.02em'>
            DeathNote
          </h1>
          <p style='color:#8A8478;margin:8px 0 0;font-size:13px'>
            A message has been waiting for you
          </p>
        </div>

        <!-- Body -->
        <div style='background:#ffffff;padding:40px 36px;border:1px solid #e5e5e5;border-top:none'>

          <h2 style='font-weight:300;font-size:26px;margin:0 0 20px;
                     color:#1a1a1a;font-style:italic;line-height:1.3'>
            {sender.FullName} left you a final message
          </h2>

          <p style='color:#555;line-height:1.8;font-size:15px;margin:0 0 12px'>
            Dear {recipient.Name},
          </p>

          <p style='color:#555;line-height:1.8;font-size:15px;margin:0 0 20px'>
            Someone who cared deeply about you wrote you a personal message
            before passing away. They wanted to make sure these words
            reached you.
          </p>

          <!-- Message preview box -->
          <div style='background:#f9f6f0;border-left:3px solid #C4A55A;
                      padding:20px 24px;margin:0 0 28px;border-radius:0 8px 8px 0'>
            <div style='font-size:12px;color:#9a8a6a;text-transform:uppercase;
                        letter-spacing:0.08em;margin-bottom:8px;font-family:sans-serif'>
              Message from
            </div>
            <div style='font-size:20px;color:#1a1a1a;font-weight:400;
                        margin-bottom:4px'>{sender.FullName}</div>
            <div style='font-size:14px;color:#888;font-style:italic'>
              &ldquo;{message.Title}&rdquo;
            </div>
          </div>

          <p style='color:#555;line-height:1.8;font-size:15px;margin:0 0 28px'>
            To read their final words, click the button below.
            A small one-time fee of <strong style='color:#1a1a1a'>₹399</strong> is
            required to unlock the message — this helps keep DeathNote
            running for others like you.
          </p>

          <!-- CTA Button -->
          <div style='text-align:center;margin:0 0 24px'>
            <a href='{unlockUrl}'
               style='background:#C4A55A;color:#0B0B0D;padding:16px 44px;
                      border-radius:100px;text-decoration:none;font-size:16px;
                      font-weight:600;display:inline-block;letter-spacing:0.02em;
                      font-family:Arial,sans-serif'>
              Unlock their message — ₹399
            </a>
          </div>

          <!-- Or copy link -->
          <p style='color:#aaa;font-size:12px;text-align:center;
                    margin:0 0 28px;line-height:1.6'>
            Or copy this link into your browser:<br/>
            <span style='color:#C4A55A;word-break:break-all'>{unlockUrl}</span>
          </p>

          <!-- Trust signals -->
          <div style='background:#f9f9f9;border:1px solid #eee;border-radius:10px;
                      padding:16px 20px;margin:0 0 8px'>
            <table width='100%' cellpadding='0' cellspacing='0'>
              <tr>
                <td style='text-align:center;padding:4px'>
                  <div style='font-size:18px'>🔒</div>
                  <div style='font-size:11px;color:#888;font-family:sans-serif;margin-top:4px'>Secure payment</div>
                </td>
                <td style='text-align:center;padding:4px'>
                  <div style='font-size:18px'>♾️</div>
                  <div style='font-size:11px;color:#888;font-family:sans-serif;margin-top:4px'>Access forever</div>
                </td>
                <td style='text-align:center;padding:4px'>
                  <div style='font-size:18px'>📧</div>
                  <div style='font-size:11px;color:#888;font-family:sans-serif;margin-top:4px'>Powered by Razorpay</div>
                </td>
              </tr>
            </table>
          </div>

        </div>

        <!-- Footer -->
        <div style='background:#f0f0f0;padding:20px;text-align:center;
                    border-radius:0 0 16px 16px'>
          <p style='color:#aaa;font-size:12px;margin:0;font-family:sans-serif'>
            DeathNote · Words that outlive you<br/>
            <span style='color:#ccc'>You received this because someone chose you as their recipient.</span>
          </p>
        </div>

      </div>
    </body>
    </html>";

        await SendAsync(recipient.Email, recipient.Name, subject, body);
    }

    // ── VERIFIER REMINDER ─────────────────────────────
    public async Task SendVerifierReminderAsync(Verifier verifier, User user)
    {
        var subject = $"Reminder: You are {user.FullName}'s trusted verifier";
        var body = $@"
        <div style='font-family:Georgia,serif;max-width:540px;margin:0 auto'>
          <div style='background:#0B0B0D;padding:28px;text-align:center;border-radius:12px 12px 0 0'>
            <h1 style='color:#C4A55A;font-weight:300;font-size:26px;margin:0'>DeathNote</h1>
          </div>
          <div style='background:#fff;padding:40px 36px;border:1px solid #e5e5e5;border-top:none'>
            <p style='color:#555;line-height:1.8'>Dear {verifier.Name},</p>
            <p style='color:#555;line-height:1.8'>
              This is a periodic reminder that you are the trusted verifier for
              <strong>{user.FullName}</strong>. Please keep this email safe.
            </p>
            <div style='text-align:center;margin:28px 0'>
              <a href='{_frontendUrl}/verifier/confirm/{verifier.Id}'
                 style='background:#C4A55A;color:#0B0B0D;padding:12px 28px;border-radius:100px;
                        text-decoration:none;font-size:14px;font-weight:500;display:inline-block'>
                My verifier link
              </a>
            </div>
          </div>
        </div>";

        await SendAsync(verifier.Email, verifier.Name, subject, body);
    }

    // ── PAYMENT CONFIRMATION ──────────────────────────
    public async Task SendPaymentConfirmationAsync(string recipientEmail, string senderName)
    {
        var subject = $"Message unlocked — {senderName}";
        var body = $@"<div style='font-family:Georgia,serif;max-width:520px;margin:0 auto'>
          <div style='background:#0B0B0D;padding:28px;text-align:center;border-radius:12px 12px 0 0'>
            <h1 style='color:#C4A55A;font-weight:300;font-size:26px;margin:0'>DeathNote</h1>
          </div>
          <div style='background:#fff;padding:40px 36px;border:1px solid #e5e5e5;border-top:none'>
            <h2 style='color:#4A8C6E;font-weight:400'>Message unlocked ✓</h2>
            <p style='color:#555;line-height:1.8'>
              You now have lifetime access to {senderName}'s final message.
              Take your time — there is no rush.
            </p>
            <div style='text-align:center;margin:28px 0'>
              <a href='{_frontendUrl}/dashboard'
                 style='background:#C4A55A;color:#0B0B0D;padding:12px 28px;border-radius:100px;
                        text-decoration:none;font-size:14px;font-weight:500;display:inline-block'>
                Read message
              </a>
            </div>
          </div>
        </div>";

        await SendAsync(recipientEmail, "", subject, body);
    }

    // ── PAYMENT RECEIVED (manual UPI) ─────────────────
    public async Task SendPaymentReceivedAsync(string recipientEmail, string upiTxnId)
    {
        var subject = "Payment received — verification in progress";
        var body = $@"<div style='font-family:Georgia,serif;max-width:520px;margin:0 auto'>
          <div style='background:#0B0B0D;padding:28px;text-align:center;border-radius:12px 12px 0 0'>
            <h1 style='color:#C4A55A;font-weight:300;font-size:26px;margin:0'>DeathNote</h1>
          </div>
          <div style='background:#fff;padding:40px 36px;border:1px solid #e5e5e5;border-top:none'>
            <h2 style='font-weight:400'>We received your payment details</h2>
            <p style='color:#555;line-height:1.8'>
              Transaction ID: <strong>{upiTxnId}</strong><br>
              Our team will verify your payment within <strong>24 hours</strong>.
              You will receive an email once the message is unlocked.
            </p>
          </div>
        </div>";

        await SendAsync(recipientEmail, "", subject, body);
    }

    // ── PRIVATE SEND ──────────────────────────────────
    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        try
        {
            var from = new EmailAddress(_fromEmail, _fromName);
            var to = new EmailAddress(toEmail, toName);
            var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlBody);
            var response = await _client.SendEmailAsync(msg);

            if (!response.IsSuccessStatusCode)
                _logger.LogError("SendGrid error {Status}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email failed to {Email}", toEmail);
        }
    }
}