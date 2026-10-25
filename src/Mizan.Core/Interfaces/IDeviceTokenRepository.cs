using Mizan.Core.Entities;

namespace Mizan.Core.Interfaces;

public interface IDeviceTokenRepository
{
    Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeviceToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> CountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(DeviceToken token, CancellationToken cancellationToken = default);
    Task DeleteByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    void Remove(DeviceToken token);
    void RemoveRange(IEnumerable<DeviceToken> tokens);
}
