using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using USP.Core.Services.Communication;

namespace USP.Infrastructure.Services.Communication;

/// <summary>
/// SMS service with multi-provider support (Twilio, AWS SNS, custom HTTP)
/// Includes rate limiting to prevent abuse
/// </summary>
public class SmsService : ISmsService
{
    private readonly ILogger<SmsService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;

    private const string RateLimitCacheKeyPrefix = "sms:ratelimit:";
    private const int MaxSmsPerHour = 5;
    private const int MaxSmsPerDay = 20;

    public SmsService(
        ILogger<SmsService> logger,
        IConfiguration _configuration,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        this._configuration = _configuration;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> SendSmsAsync(string phoneNumber, string message)
    {
        if (!await CheckRateLimitAsync(phoneNumber))
        {
            _logger.LogWarning("SMS rate limit exceeded for phone number: {PhoneNumber}", MaskPhoneNumber(phoneNumber));
            return false;
        }

        var provider = _configuration["Sms:Provider"] ?? "Twilio";

        bool sent = provider.ToLowerInvariant() switch
        {
            "twilio" => await SendViaTwilioAsync(phoneNumber, message, false),
            "awssns" => await SendViaAwsSnsAsync(phoneNumber, message, false),
            "http" => await SendViaHttpAsync(phoneNumber, message, false),
            _ => throw new InvalidOperationException($"Unsupported SMS provider: {provider}")
        };

        if (sent)
        {
            await RecordSmsSentAsync(phoneNumber);
        }

        return sent;
    }

    public async Task<bool> SendVoiceCallAsync(string phoneNumber, string message)
    {
        if (!await CheckRateLimitAsync(phoneNumber))
        {
            _logger.LogWarning("Voice call rate limit exceeded for phone number: {PhoneNumber}", MaskPhoneNumber(phoneNumber));
            return false;
        }

        var provider = _configuration["Sms:Provider"] ?? "Twilio";

        bool sent = provider.ToLowerInvariant() switch
        {
            "twilio" => await SendViaTwilioAsync(phoneNumber, message, true),
            "awssns" => false, // AWS SNS doesn't support voice calls directly
            "http" => await SendViaHttpAsync(phoneNumber, message, true),
            _ => throw new InvalidOperationException($"Unsupported voice provider: {provider}")
        };

        if (sent)
        {
            await RecordSmsSentAsync(phoneNumber);
        }

        return sent;
    }

    public async Task<bool> SendOtpSmsAsync(string phoneNumber, string code, int expirationMinutes = 5)
    {
        var message = $"Your verification code is: {code}. This code will expire in {expirationMinutes} minutes.";
        return await SendSmsAsync(phoneNumber, message);
    }

    public async Task<bool> SendOtpVoiceAsync(string phoneNumber, string code)
    {
        // Format code for voice (space out digits for clarity)
        var spokenCode = string.Join(" ", code.ToCharArray());
        var message = $"Your verification code is: {spokenCode}. I repeat: {spokenCode}.";
        return await SendVoiceCallAsync(phoneNumber, message);
    }

    public async Task<string> SendVerificationCodeAsync(string phoneNumber)
    {
        var code = GenerateOtpCode();
        var sent = await SendOtpSmsAsync(phoneNumber, code, 10);

        if (!sent)
        {
            throw new InvalidOperationException("Failed to send verification code");
        }

        return code;
    }

    #region Private Helper Methods

    private async Task<bool> CheckRateLimitAsync(string phoneNumber)
    {
        var hourKey = $"{RateLimitCacheKeyPrefix}hour:{phoneNumber}";
        var dayKey = $"{RateLimitCacheKeyPrefix}day:{phoneNumber}";

        var hourCount = _cache.GetOrCreate(hourKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return 0;
        });

        var dayCount = _cache.GetOrCreate(dayKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
            return 0;
        });

        if (hourCount >= MaxSmsPerHour)
        {
            _logger.LogWarning("Hourly SMS rate limit exceeded for {PhoneNumber}: {Count}/{Max}",
                MaskPhoneNumber(phoneNumber), hourCount, MaxSmsPerHour);
            return false;
        }

        if (dayCount >= MaxSmsPerDay)
        {
            _logger.LogWarning("Daily SMS rate limit exceeded for {PhoneNumber}: {Count}/{Max}",
                MaskPhoneNumber(phoneNumber), dayCount, MaxSmsPerDay);
            return false;
        }

        return true;
    }

    private async Task RecordSmsSentAsync(string phoneNumber)
    {
        var hourKey = $"{RateLimitCacheKeyPrefix}hour:{phoneNumber}";
        var dayKey = $"{RateLimitCacheKeyPrefix}day:{phoneNumber}";

        var hourCount = _cache.Get<int>(hourKey);
        var dayCount = _cache.Get<int>(dayKey);

        _cache.Set(hourKey, hourCount + 1, TimeSpan.FromHours(1));
        _cache.Set(dayKey, dayCount + 1, TimeSpan.FromDays(1));

        await Task.CompletedTask;
    }

    private async Task<bool> SendViaTwilioAsync(string phoneNumber, string message, bool isVoice)
    {
        try
        {
            var accountSid = _configuration["Sms:Twilio:AccountSid"];
            var authToken = _configuration["Sms:Twilio:AuthToken"];
            var fromNumber = _configuration["Sms:Twilio:FromNumber"];

            if (string.IsNullOrEmpty(accountSid) || string.IsNullOrEmpty(authToken) || string.IsNullOrEmpty(fromNumber))
            {
                _logger.LogError("Twilio configuration is incomplete");
                return false;
            }

            // Note: For production, use Twilio SDK (Twilio.Rest.Api.V2010.Account.MessageResource.Create)
            // This is a simplified implementation showing the HTTP approach

            var client = _httpClientFactory.CreateClient();
            var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{accountSid}:{authToken}"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

            var endpoint = isVoice
                ? $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Calls.json"
                : $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";

            var content = isVoice
                ? new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("From", fromNumber),
                    new KeyValuePair<string, string>("To", phoneNumber),
                    new KeyValuePair<string, string>("Twiml", $"<Response><Say>{message}</Say></Response>")
                })
                : new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("From", fromNumber),
                    new KeyValuePair<string, string>("To", phoneNumber),
                    new KeyValuePair<string, string>("Body", message)
                });

            var response = await client.PostAsync(endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SMS/Voice sent via Twilio to {PhoneNumber}", MaskPhoneNumber(phoneNumber));
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("Twilio API error: {StatusCode} - {Error}", response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS/Voice via Twilio to {PhoneNumber}", MaskPhoneNumber(phoneNumber));
            return false;
        }
    }

    private async Task<bool> SendViaAwsSnsAsync(string phoneNumber, string message, bool isVoice)
    {
        // Note: For production, use AWS SDK (Amazon.SimpleNotificationService.AmazonSimpleNotificationServiceClient)
        // This is a placeholder showing the general approach

        try
        {
            _logger.LogInformation("AWS SNS provider not fully implemented. Would send to {PhoneNumber}", MaskPhoneNumber(phoneNumber));

            // TODO: Implement AWS SNS integration
            // var snsClient = new AmazonSimpleNotificationServiceClient(region);
            // var request = new PublishRequest
            // {
            //     PhoneNumber = phoneNumber,
            //     Message = message
            // };
            // var response = await snsClient.PublishAsync(request);

            await Task.CompletedTask;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS via AWS SNS");
            return false;
        }
    }

    private async Task<bool> SendViaHttpAsync(string phoneNumber, string message, bool isVoice)
    {
        try
        {
            var endpoint = _configuration[$"Sms:Http:{(isVoice ? "VoiceEndpoint" : "SmsEndpoint")}"];
            var apiKey = _configuration["Sms:Http:ApiKey"];

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("HTTP SMS provider configuration is incomplete");
                return false;
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

            var payload = new
            {
                to = phoneNumber,
                message = message,
                type = isVoice ? "voice" : "sms"
            };

            var response = await client.PostAsJsonAsync(endpoint, payload);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SMS/Voice sent via HTTP provider to {PhoneNumber}", MaskPhoneNumber(phoneNumber));
                return true;
            }

            _logger.LogError("HTTP SMS provider error: {StatusCode}", response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS/Voice via HTTP provider");
            return false;
        }
    }

    private static string GenerateOtpCode()
    {
        var bytes = new byte[3];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        var code = BitConverter.ToUInt32(new byte[] { bytes[0], bytes[1], bytes[2], 0 }) % 1000000;
        return code.ToString("D6");
    }

    private static string MaskPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 4)
        {
            return "****";
        }

        return $"****{phoneNumber.Substring(phoneNumber.Length - 4)}";
    }

    #endregion
}
