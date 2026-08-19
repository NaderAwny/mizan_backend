namespace Mizan.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IShopRepository Shops { get; }
    IRefreshTokenRepository RefreshTokens { get; }
    IOtpCodeRepository OtpCodes { get; }
    IContactRepository Contacts { get; }
    ITransactionRepository Transactions { get; }
    IInstallmentRepository Installments { get; }
    INotificationRepository Notifications { get; }
    IInstallmentReminderLogRepository InstallmentReminderLogs { get; }
    IPeriodicReportRepository PeriodicReports { get; }
    IVoiceNoteRepository VoiceNotes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
