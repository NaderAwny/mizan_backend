using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Mizan.Application.DTOs.Auth;

public class SendOtpRequest
{
    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صالحة")]
    [MaxLength(254, ErrorMessage = "البريد الإلكتروني لا يمكن أن يتجاوز 254 حرف")]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}
