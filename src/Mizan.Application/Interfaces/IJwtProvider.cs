using Mizan.Core.Entities;

namespace Mizan.Application.Interfaces;

public interface IJwtProvider
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    int? ValidateTokenAndGetUserId(string token);
    int AccessTokenExpirationSeconds { get; }
    int RefreshTokenExpirationDays { get; }
}
