namespace dnotes_backend.Services;

public interface ISmsService
{
    Task SendOtpSmsAsync(string phoneNumber, string otp);
}

/// <summary>
/// Uses Fast2SMS (popular Indian SMS gateway, free tier available)
/// Alternative: Twilio, MSG91, or any other SMS provider
/// Sign up at: fast2sms.com
/// </summary>
public class SmsService : ISmsService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmsService> _logger;
    private readonly HttpClient _http;

    public SmsService(IConfiguration config, ILogger<SmsService> logger)
    {
        _config = config;
        _logger = logger;
        _http = new HttpClient();
    }

    public async Task SendOtpSmsAsync(string phoneNumber, string otp)
    {
        var apiKey = _config["Fast2SMS:ApiKey"];

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Fast2SMS ApiKey not configured — SMS not sent");
            return;
        }

        try
        {
            // Remove +91 or country code if present
            var cleanPhone = phoneNumber.Replace("+91", "").Replace(" ", "").Trim();

            var payload = new
            {
                route = "otp",
                variables_values = otp,
                numbers = cleanPhone,
                flash = 0
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("authorization", apiKey);

            var response = await _http.PostAsync(
                "https://www.fast2sms.com/dev/bulkV2", content);

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogInformation(
                "SMS sent to {Phone}: {Status}", phoneNumber, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {Phone}", phoneNumber);
            // Don't throw — SMS failure should not block the user flow
        }
    }
}