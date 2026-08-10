using Microsoft.EntityFrameworkCore;
using Mizan.Core.Entities;
using Mizan.Core.Interfaces;

namespace Mizan.Infrastructure.Persistence.Repositories;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(MizanDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByWhatsAppNumberAsync(string whatsappNumber, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(u => u.Shop)
            .FirstOrDefaultAsync(u => u.WhatsAppNumber == whatsappNumber, cancellationToken);
    }

    public async Task<User?> GetWithShopAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(u => u.Shop)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }
}
