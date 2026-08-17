using Mizan.Core.Entities;

namespace Mizan.Core.Interfaces;

public interface IShopRepository : IBaseRepository<Shop>
{
    Task<Shop?> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default);
}
