using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Mizan.Application.DTOs.Auth;

public class LogoutRequest
{
    [Required(ErrorMessage = "رمز التحديث مطلوب")]
    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;
}
