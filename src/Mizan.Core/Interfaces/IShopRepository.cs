using Mizan.Core.Entities;

namespace Mizan.Core.Interfaces;

public interface IShopRepository : IBaseRepository<Shop>
{
    Task<Shop?> GetByOwnerIdAsync(int ownerId, CancellationToken cancellationToken = default);
}
