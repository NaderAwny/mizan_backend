using Microsoft.EntityFrameworkCore;
using Mizan.Core.Entities;
using Mizan.Core.Interfaces;

namespace Mizan.Infrastructure.Persistence.Repositories;

public class DeviceTokenRepository : BaseRepository<DeviceToken>, IDeviceTokenRepository
{
    public DeviceTokenRepository(MizanDbContext context) : base(context)
    {
    }

    public async Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(d => d.Token == token, cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(d => d.UserId == userId)
            .OrderBy(d => d.LastSeenAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.CountAsync(d => d.UserId == userId, cancellationToken);
    }

    public async Task DeleteByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var existing = await _dbSet.FirstOrDefaultAsync(d => d.Token == token, cancellationToken);
        if (existing != null)
        {
            _dbSet.Remove(existing);
        }
    }

    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userTokens = await _dbSet.Where(d => d.UserId == userId).ToListAsync(cancellationToken);
        if (userTokens.Count > 0)
        {
            _dbSet.RemoveRange(userTokens);
        }
    }

    public void Remove(DeviceToken token)
    {
        _dbSet.Remove(token);
    }

    public void RemoveRange(IEnumerable<DeviceToken> tokens)
    {
        _dbSet.RemoveRange(tokens);
    }
}
