namespace Mizan.Core.Interfaces;

public interface IUserRepository : IBaseRepository<Core.Entities.User>
{
    Task<Core.Entities.User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Core.Entities.User?> GetWithShopAsync(Guid userId, CancellationToken cancellationToken = default);
}
