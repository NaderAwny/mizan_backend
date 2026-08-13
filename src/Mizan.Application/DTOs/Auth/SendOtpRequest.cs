using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Mizan.Application.DTOs.Auth;

public class SendOtpRequest
{
    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صالحة")]
    [MaxLength(100, ErrorMessage = "البريد الإلكتروني لا يمكن أن يتجاوز 100 حرف")]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonIgnore]
    public string TargetIdentifier => Email.Trim().ToLowerInvariant();
}
