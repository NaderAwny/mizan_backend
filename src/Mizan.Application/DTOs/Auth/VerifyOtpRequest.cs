using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Mizan.Application.DTOs.Auth;

public class VerifyOtpRequest
{
    [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صالحة")]
    [MaxLength(254, ErrorMessage = "البريد الإلكتروني لا يمكن أن يتجاوز 254 حرف")]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "كود التحقق مطلوب")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "كود التحقق يجب أن يتكون من 6 أرقام")]
    [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "Code must be 6 digits")]
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;
}
