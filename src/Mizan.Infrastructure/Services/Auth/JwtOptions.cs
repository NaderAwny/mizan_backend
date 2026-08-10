namespace Mizan.Infrastructure.Services.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = "MizanSecretSuperKeyForJwtSigning_MustBeAtLeast32BytesLong!";
    public string Issuer { get; set; } = "MizanBackend";
    public string Audience { get; set; } = "MizanApp";
    public int AccessTokenExpirationDays { get; set; } = 7;
    public int RefreshTokenExpirationDays { get; set; } = 30;
}
