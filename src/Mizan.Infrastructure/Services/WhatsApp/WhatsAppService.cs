using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Services.WhatsApp;

public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = configuration.GetSection(WhatsAppOptions.SectionName).Get<WhatsAppOptions>() ?? new WhatsAppOptions();
    }

    public async Task<bool> SendOtpMessageAsync(string toWhatsAppNumber, string otpCode, CancellationToken cancellationToken = default)
    {
        var message = $"كود التحقق الخاص بك في تطبيق ميزان هو: {otpCode}\nصالح لمدة دقيقتين. لا تشارك الكود مع أي شخص.";
        return await SendTextMessageAsync(toWhatsAppNumber, message, cancellationToken);
    }

    public async Task<bool> SendTextMessageAsync(string toWhatsAppNumber, string message, CancellationToken cancellationToken = default)
    {
        // Format recipient phone number for WhatsApp international standard (Egypt: +20...)
        var formattedRecipient = FormatToInternationalNumber(toWhatsAppNumber);

        if (string.IsNullOrWhiteSpace(_options.AccessToken) || string.IsNullOrWhiteSpace(_options.PhoneNumberId) || _options.UseMockInDevelopment)
        {
            _logger.LogInformation("[WhatsApp Service - Mock/Dev Mode] Sent WhatsApp message to {Recipient}: {Message}", formattedRecipient, message);
            return true;
        }

        try
        {
            var url = $"https://graph.facebook.com/{_options.ApiVersion}/{_options.PhoneNumberId}/messages";

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = formattedRecipient,
                type = "text",
                text = new { preview_url = false, body = message }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send WhatsApp message via Meta Cloud API. Status: {StatusCode}, Response: {Error}", response.StatusCode, errorBody);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending WhatsApp message to {Recipient}", formattedRecipient);
            return false;
        }
    }

    private static string FormatToInternationalNumber(string phone)
    {
        phone = phone.Trim().Replace(" ", "").Replace("-", "").Replace("+", "");
        if (phone.StartsWith("0"))
        {
            phone = "20" + phone[1..];
        }
        else if (!phone.StartsWith("20"))
        {
            phone = "20" + phone;
        }
        return phone;
    }
}
