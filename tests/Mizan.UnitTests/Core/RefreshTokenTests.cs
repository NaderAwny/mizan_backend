using Mizan.Core.Entities;
using Xunit;

namespace Mizan.UnitTests.Core;

public class RefreshTokenTests
{
    private static readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void RefreshToken_ShouldBeActiveWhenCreated()
    {
        // Act
        var token = RefreshToken.Create(_userId, "sample-token-string", DateTime.UtcNow.AddDays(30));

        // Assert
        Assert.True(token.IsActive);
        Assert.False(token.IsRevoked);
        Assert.False(token.IsExpired);
    }

    [Fact]
    public void RefreshToken_Revoke_ShouldDeactivateToken()
    {
        // Arrange
        var token = RefreshToken.Create(_userId, "sample-token-string", DateTime.UtcNow.AddDays(30));

        // Act
        token.Revoke("replacement-token");

        // Assert
        Assert.False(token.IsActive);
        Assert.True(token.IsRevoked);
        // C4: ReplacedByTokenHash stores the SHA-256 hash of the replacement token
        var expectedHash = RefreshToken.HashToken("replacement-token");
        Assert.Equal(expectedHash, token.ReplacedByTokenHash);
    }

    [Fact]
    public void RefreshToken_WhenExpired_ShouldNotBeActive()
    {
        // Arrange
        var token = RefreshToken.Create(_userId, "sample-token-string", DateTime.UtcNow.AddDays(-1));

        // Assert
        Assert.False(token.IsActive);
        Assert.True(token.IsExpired);
    }
}
