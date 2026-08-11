using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Mizan.Application.DTOs.Auth;

public class VerifyOtpRequest
{
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("whatsappNumber")]
    public string? WhatsAppNumber { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber
    {
        get => WhatsAppNumber;
        set => WhatsAppNumber = value;
    }

    [Required(ErrorMessage = "كود التحقق مطلوب")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "كود التحقق يجب أن يتكون من 6 أرقام")]
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonIgnore]
    public string TargetIdentifier => !string.IsNullOrWhiteSpace(Email) ? Email.Trim() : (WhatsAppNumber?.Trim() ?? string.Empty);
}
