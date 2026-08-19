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

    private string _code = string.Empty;

    [JsonPropertyName("code")]
    public string Code
    {
        get => _code;
        set => _code = value ?? string.Empty;
    }

    [JsonPropertyName("otpCode")]
    public string? OtpCode
    {
        get => _code;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _code = value;
            }
        }
    }
}

