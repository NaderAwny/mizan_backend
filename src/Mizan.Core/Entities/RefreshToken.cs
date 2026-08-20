using System.Security.Cryptography;
using System.Text;

namespace Mizan.Core.Entities;

public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    /// <summary>SHA-256 hash of the raw token. The raw token is NEVER stored.</summary>
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;

    // Navigation property
    public User User { get; private set; } = null!;

    private RefreshToken() { }

    /// <summary>Compute SHA-256 hex of a raw token string.</summary>
    public static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static RefreshToken Create(Guid userId, string rawToken, DateTime expiresAt)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(rawToken),
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Revoke(string? replacedByRawToken = null)
    {
        RevokedAt = DateTime.UtcNow;
        ReplacedByTokenHash = replacedByRawToken != null ? HashToken(replacedByRawToken) : null;
    }
}
