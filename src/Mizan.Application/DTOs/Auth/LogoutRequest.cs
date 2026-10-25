using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Mizan.Application.DTOs.Auth;

public class LogoutRequest
{
    [Required(ErrorMessage = "رمز التحديث مطلوب")]
    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("deviceToken")]
    [StringLength(512, ErrorMessage = "رمز الجهاز يجب ألا يتجاوز 512 حرفاً")]
    public string? DeviceToken { get; set; }
}
