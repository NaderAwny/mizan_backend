using Microsoft.EntityFrameworkCore;
using Mizan.Core.Entities;
using Mizan.Core.Interfaces;

namespace Mizan.Infrastructure.Persistence.Repositories;

public class InstallmentRepository : IInstallmentRepository
{
    private readonly MizanDbContext _context;
    private readonly DbSet<Installment> _dbSet;

    public InstallmentRepository(MizanDbContext context)
    {
        _context = context;
        _dbSet = context.Set<Installment>();
    }

    public async Task<IReadOnlyList<Installment>> GetByTransactionIdAsync(int transactionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(i => i.TransactionId == transactionId)
            .OrderBy(i => i.InstallmentNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<Installment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(i => i.Transaction)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Installment> installments, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddRangeAsync(installments, cancellationToken);
    }

    public void Update(Installment installment)
    {
        _dbSet.Update(installment);
    }
}
