using Microsoft.EntityFrameworkCore;
using Mizan.Core.Entities;
using Mizan.Core.Interfaces;

namespace Mizan.Infrastructure.Persistence.Repositories;

public class ContactRepository : IContactRepository
{
    private readonly MizanDbContext _context;
    private readonly DbSet<Contact> _dbSet;

    public ContactRepository(MizanDbContext context)
    {
        _context = context;
        _dbSet = context.Set<Contact>();
    }

    /// <summary>
    /// Returns the contact only if it exists AND belongs to the specified owner.
    /// Returns null in all other cases — never exposes another user's contact.
    /// </summary>
    public async Task<Contact?> GetByIdAsync(Guid id, Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.Id == id && c.OwnerUserId == ownerUserId, cancellationToken);
    }

    public async Task<(IReadOnlyList<Contact> Items, int TotalCount)> GetPagedByOwnerAsync(
        Guid ownerUserId,
        int page,
        int pageSize,
        string? searchTerm,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Where(c => c.OwnerUserId == ownerUserId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Contact contact, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(contact, cancellationToken);
    }

    public void Update(Contact contact)
    {
        _dbSet.Update(contact);
    }
}
