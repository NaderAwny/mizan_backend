using Microsoft.EntityFrameworkCore.Storage;
using Mizan.Core.Interfaces;
using Mizan.Infrastructure.Persistence.Repositories;

namespace Mizan.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly MizanDbContext _context;
    private IDbContextTransaction? _transaction;

    private IUserRepository? _users;
    private IShopRepository? _shops;
    private IRefreshTokenRepository? _refreshTokens;
    private IOtpCodeRepository? _otpCodes;
    private IContactRepository? _contacts;
    private ITransactionRepository? _transactions;
    private IInstallmentRepository? _installments;
    private INotificationRepository? _notifications;
    private IInstallmentReminderLogRepository? _installmentReminderLogs;
    private IPeriodicReportRepository? _periodicReports;
    private IVoiceNoteRepository? _voiceNotes;

    public UnitOfWork(MizanDbContext context)
    {
        _context = context;
    }

    public IUserRepository Users => _users ??= new UserRepository(_context);
    public IShopRepository Shops => _shops ??= new ShopRepository(_context);
    public IRefreshTokenRepository RefreshTokens => _refreshTokens ??= new RefreshTokenRepository(_context);
    public IOtpCodeRepository OtpCodes => _otpCodes ??= new OtpCodeRepository(_context);
    public IContactRepository Contacts => _contacts ??= new ContactRepository(_context);
    public ITransactionRepository Transactions => _transactions ??= new TransactionRepository(_context);
    public IInstallmentRepository Installments => _installments ??= new InstallmentRepository(_context);
    public INotificationRepository Notifications => _notifications ??= new NotificationRepository(_context);
    public IInstallmentReminderLogRepository InstallmentReminderLogs => _installmentReminderLogs ??= new InstallmentReminderLogRepository(_context);
    public IPeriodicReportRepository PeriodicReports => _periodicReports ??= new PeriodicReportRepository(_context);
    public IVoiceNoteRepository VoiceNotes => _voiceNotes ??= new VoiceNoteRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
