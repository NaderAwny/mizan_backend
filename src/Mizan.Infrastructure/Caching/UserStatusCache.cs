using Microsoft.Extensions.Caching.Memory;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Caching;

public class UserStatusCache : IUserStatusCache
{
    private readonly IMemoryCache _memoryCache;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromSeconds(30);

    public UserStatusCache(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public async Task<bool> GetOrSetUserActiveStatusAsync(Guid userId, Func<Guid, Task<bool>> statusFactory, TimeSpan? expiration = null)
    {
        var cacheKey = GetCacheKey(userId);

        if (_memoryCache.TryGetValue(cacheKey, out bool isActive))
        {
            return isActive;
        }

        isActive = await statusFactory(userId);
        _memoryCache.Set(cacheKey, isActive, expiration ?? DefaultExpiration);
        return isActive;
    }

    public void InvalidateUserStatus(Guid userId)
    {
        var cacheKey = GetCacheKey(userId);
        _memoryCache.Remove(cacheKey);
    }

    private static string GetCacheKey(Guid userId) => $"user_active_status_{userId}";
}
