using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mizan.Application.DTOs.Devices;
using Mizan.Application.Services;
using Mizan.Core.Entities;
using Mizan.Core.Enums;
using Mizan.Core.Exceptions;
using Mizan.Infrastructure.Persistence;
using Xunit;

namespace Mizan.UnitTests.Services;

public class DeviceTokenServiceTests
{
    private static MizanDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MizanDbContext(options);
    }

    private static (DeviceTokenService Service, MizanDbContext Db) CreateService()
    {
        var db = CreateDb();
        var uow = new UnitOfWork(db);
        var service = new DeviceTokenService(uow, NullLogger<DeviceTokenService>.Instance);
        return (service, db);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task RegisterAsync_EmptyToken_ThrowsBadRequestException(string? token)
    {
        var (service, _) = CreateService();
        var request = new RegisterDeviceTokenRequest { Token = token!, Platform = DevicePlatform.Android };
        await Assert.ThrowsAsync<BadRequestException>(() => service.RegisterAsync(Guid.NewGuid(), request));
    }

    [Fact]
    public async Task RegisterAsync_TokenExceeds512_ThrowsBadRequestException()
    {
        var (service, _) = CreateService();
        var request = new RegisterDeviceTokenRequest { Token = new string('a', 513), Platform = DevicePlatform.Android };
        await Assert.ThrowsAsync<BadRequestException>(() => service.RegisterAsync(Guid.NewGuid(), request));
    }

    [Fact]
    public async Task RegisterAsync_NewToken_AddsAndSaves()
    {
        var (service, db) = CreateService();
        var userId = Guid.NewGuid();
        var request = new RegisterDeviceTokenRequest
        {
            Token = "valid-token-123",
            Platform = DevicePlatform.Android,
            DeviceId = "dev-1"
        };

        await service.RegisterAsync(userId, request);

        var tokenInDb = await db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == "valid-token-123");
        Assert.NotNull(tokenInDb);
        Assert.Equal(userId, tokenInDb.UserId);
        Assert.Equal(DevicePlatform.Android, tokenInDb.Platform);
        Assert.Equal("dev-1", tokenInDb.DeviceId);
    }

    [Fact]
    public async Task RegisterAsync_ExistingTokenSameUser_TouchesToken()
    {
        var (service, db) = CreateService();
        var userId = Guid.NewGuid();
        var existing = DeviceToken.Create(userId, "token-abc", DevicePlatform.Android, "dev-1");
        db.DeviceTokens.Add(existing);
        await db.SaveChangesAsync();

        var request = new RegisterDeviceTokenRequest
        {
            Token = "token-abc",
            Platform = DevicePlatform.iOS,
            DeviceId = "dev-1-updated"
        };

        await service.RegisterAsync(userId, request);

        var count = await db.DeviceTokens.CountAsync(t => t.UserId == userId);
        Assert.Equal(1, count);

        var updated = await db.DeviceTokens.FirstAsync(t => t.Token == "token-abc");
        Assert.Equal(DevicePlatform.iOS, updated.Platform);
        Assert.Equal("dev-1-updated", updated.DeviceId);
    }

    [Fact]
    public async Task RegisterAsync_ExistingTokenDifferentUser_ReassignsToken()
    {
        var (service, db) = CreateService();
        var oldUser = Guid.NewGuid();
        var newUser = Guid.NewGuid();
        var existing = DeviceToken.Create(oldUser, "token-shared", DevicePlatform.Android, "device-x");
        db.DeviceTokens.Add(existing);
        await db.SaveChangesAsync();

        var request = new RegisterDeviceTokenRequest
        {
            Token = "token-shared",
            Platform = DevicePlatform.iOS,
            DeviceId = "device-x-ios"
        };

        await service.RegisterAsync(newUser, request);

        var updated = await db.DeviceTokens.FirstAsync(t => t.Token == "token-shared");
        Assert.Equal(newUser, updated.UserId);
        Assert.Equal(DevicePlatform.iOS, updated.Platform);
        Assert.Equal("device-x-ios", updated.DeviceId);
    }

    [Fact]
    public async Task RegisterAsync_Cap10Tokens_RemovesOldestWhenExceeded()
    {
        var (service, db) = CreateService();
        var userId = Guid.NewGuid();

        for (int i = 0; i < 10; i++)
        {
            var t = DeviceToken.Create(userId, $"tok-{i:D2}", DevicePlatform.Android, $"dev-{i}");
            db.DeviceTokens.Add(t);
        }
        await db.SaveChangesAsync();

        var request = new RegisterDeviceTokenRequest
        {
            Token = "tok-new-11",
            Platform = DevicePlatform.Android
        };

        await service.RegisterAsync(userId, request);

        var tokens = await db.DeviceTokens.Where(t => t.UserId == userId).ToListAsync();
        Assert.Equal(10, tokens.Count);
        Assert.Contains(tokens, t => t.Token == "tok-new-11");
    }

    [Fact]
    public async Task UnregisterAsync_TokenExistsForUser_RemovesAndSaves()
    {
        var (service, db) = CreateService();
        var userId = Guid.NewGuid();
        var token = DeviceToken.Create(userId, "token-to-remove", DevicePlatform.Android);
        db.DeviceTokens.Add(token);
        await db.SaveChangesAsync();

        await service.UnregisterAsync(userId, "token-to-remove");

        var exists = await db.DeviceTokens.AnyAsync(t => t.Token == "token-to-remove");
        Assert.False(exists);
    }

    [Fact]
    public async Task UnregisterAsync_TokenBelongsToOtherUser_DoesNotRemove()
    {
        var (service, db) = CreateService();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var token = DeviceToken.Create(ownerId, "token-other", DevicePlatform.Android);
        db.DeviceTokens.Add(token);
        await db.SaveChangesAsync();

        await service.UnregisterAsync(otherUserId, "token-other");

        var exists = await db.DeviceTokens.AnyAsync(t => t.Token == "token-other");
        Assert.True(exists);
    }

    [Fact]
    public async Task UnregisterAsync_TokenDoesNotExist_NoOp()
    {
        var (service, _) = CreateService();
        await service.UnregisterAsync(Guid.NewGuid(), "non-existent");
    }
}
