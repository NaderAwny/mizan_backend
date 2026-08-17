using Mizan.Core.Entities;

namespace Mizan.Application.Interfaces;

public interface IJwtProvider
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    Guid? ValidateTokenAndGetUserId(string token);
    int AccessTokenExpirationSeconds { get; }
    int RefreshTokenExpirationDays { get; }
}
