using Mizan.Core.Enums;

namespace Mizan.Core.Entities;

public class DeviceToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DevicePlatform Platform { get; private set; }
    public string? DeviceId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime LastSeenAt { get; private set; }

    // Navigation property
    public User? User { get; private set; }

    private DeviceToken() { } // Required for EF Core

    public static DeviceToken Create(Guid userId, string token, DevicePlatform platform, string? deviceId = null)
    {
        var now = DateTime.UtcNow;
        return new DeviceToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            Platform = platform,
            DeviceId = deviceId,
            CreatedAt = now,
            UpdatedAt = now,
            LastSeenAt = now
        };
    }

    public void Touch(DevicePlatform? platform = null, string? deviceId = null)
    {
        if (platform.HasValue) Platform = platform.Value;
        if (!string.IsNullOrWhiteSpace(deviceId)) DeviceId = deviceId;
        LastSeenAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reassign(Guid newUserId, DevicePlatform? platform = null, string? deviceId = null)
    {
        UserId = newUserId;
        if (platform.HasValue) Platform = platform.Value;
        if (!string.IsNullOrWhiteSpace(deviceId)) DeviceId = deviceId;
        LastSeenAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
