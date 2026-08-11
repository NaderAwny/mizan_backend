using System.Text.Json.Serialization;

namespace Mizan.Application.DTOs.Auth;

public class SendOtpRequest
{
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("whatsappNumber")]
    public string? WhatsAppNumber
    {
        get => PhoneNumber;
        set => PhoneNumber = value;
    }

    [JsonIgnore]
    public string TargetIdentifier => !string.IsNullOrWhiteSpace(Email) ? Email.Trim() : (PhoneNumber?.Trim() ?? string.Empty);
}
