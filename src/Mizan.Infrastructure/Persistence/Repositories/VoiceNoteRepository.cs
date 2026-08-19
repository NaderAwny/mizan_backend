using Microsoft.EntityFrameworkCore;
using Mizan.Core.Entities;
using Mizan.Core.Interfaces;

namespace Mizan.Infrastructure.Persistence.Repositories;

public class VoiceNoteRepository : IVoiceNoteRepository
{
    private readonly MizanDbContext _context;
    private readonly DbSet<VoiceNote> _dbSet;

    public VoiceNoteRepository(MizanDbContext context)
    {
        _context = context;
        _dbSet   = context.Set<VoiceNote>();
    }

    public async Task<VoiceNote?> GetByIdAsync(
        Guid id, Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(v => v.Contact)
            .FirstOrDefaultAsync(
                v => v.Id == id && v.OwnerUserId == ownerUserId && v.IsActive,
                cancellationToken);
    }

    public async Task<(IReadOnlyList<VoiceNote> Items, int TotalCount)> GetPagedByShopAsync(
        Guid shopId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(v => v.Contact)
            .Where(v => v.ShopId == shopId && v.IsActive)
            .OrderByDescending(v => v.OperationDate);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return ((IReadOnlyList<VoiceNote>)items, total);
    }

    public async Task AddAsync(VoiceNote voiceNote, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(voiceNote, cancellationToken);
    }
}
