using System.ComponentModel.DataAnnotations;

namespace Mizan.Application.DTOs.Auth;

public class RefreshTokenRequest
{
    [Required(ErrorMessage = "Access token مطلوب")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Refresh token مطلوب")]
    public string RefreshToken { get; set; } = string.Empty;
}
