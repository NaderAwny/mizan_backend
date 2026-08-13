using System.Text.Json.Serialization;

namespace Mizan.Application.DTOs.Auth;

public class OtpResponse
{
    [JsonPropertyName("otpSent")]
    public bool OtpSent { get; set; }

    [JsonPropertyName("expiresInSeconds")]
    public int ExpiresInSeconds { get; set; } = 120;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
