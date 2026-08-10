using Mizan.Core.Entities;

namespace Mizan.Core.Interfaces;

public interface IUserRepository : IBaseRepository<User>
{
    Task<User?> GetByWhatsAppNumberAsync(string whatsappNumber, CancellationToken cancellationToken = default);
    Task<User?> GetWithShopAsync(int userId, CancellationToken cancellationToken = default);
}
