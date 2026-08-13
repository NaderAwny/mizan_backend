using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Mizan.Application.DTOs.Auth;

public class VerifyOtpRequest
{
    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صالحة")]
    [MaxLength(100, ErrorMessage = "البريد الإلكتروني لا يمكن أن يتجاوز 100 حرف")]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "كود التحقق مطلوب")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "كود التحقق يجب أن يتكون من 6 أرقام بالضبط")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "كود التحقق يجب أن يتكون من أرقام فقط")]
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonIgnore]
    public string TargetIdentifier => Email.Trim().ToLowerInvariant();
}
