namespace Mizan.Infrastructure.Services.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "MizanBackend";
    public string Audience { get; set; } = "MizanApp";
    // H1: Changed from days to minutes — 7 days was dangerously long
    public int AccessTokenExpirationMinutes { get; set; } = 30;
    public int RefreshTokenExpirationDays { get; set; } = 30;
}
