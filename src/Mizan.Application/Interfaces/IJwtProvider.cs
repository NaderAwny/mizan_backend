using Mizan.Core.Entities;

namespace Mizan.Application.Interfaces;

public interface IJwtProvider
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    // M6: ValidateTokenAndGetUserId removed — dead code that bypassed lifetime validation
    int AccessTokenExpirationSeconds { get; }
    int RefreshTokenExpirationDays { get; }
}
