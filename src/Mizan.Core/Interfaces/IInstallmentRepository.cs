using Mizan.Core.Entities;

namespace Mizan.Core.Interfaces;

public interface IInstallmentRepository
{
    Task<IReadOnlyList<Installment>> GetByTransactionIdAsync(int transactionId, CancellationToken cancellationToken = default);

    Task<Installment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Installment>> GetPendingByDueDateAsync(DateTime dueDate, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<Installment> installments, CancellationToken cancellationToken = default);

    void Update(Installment installment);
}
