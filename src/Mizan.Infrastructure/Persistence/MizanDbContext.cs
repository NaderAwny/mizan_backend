using Microsoft.EntityFrameworkCore;
using Mizan.Core.Entities;
using Mizan.Infrastructure.Persistence.Converters;

namespace Mizan.Infrastructure.Persistence;

public class MizanDbContext : DbContext
{
    public MizanDbContext(DbContextOptions<MizanDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<InstallmentReminderLog> InstallmentReminderLogs => Set<InstallmentReminderLog>();
    public DbSet<PeriodicReport> PeriodicReports => Set<PeriodicReport>();
    public DbSet<VoiceNote> VoiceNotes => Set<VoiceNote>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    // Restore DateTimeKind.Utc for every DateTime/DateTime? read back from SQL Server (datetime2
    // does not store Kind, so values come back Unspecified and would otherwise serialize without a
    // trailing "Z"). This is a model-level conversion only: the store type stays datetime2, so no
    // migration is produced and no stored data changes. Date-only business fields (installment due
    // dates, transaction dates, voice-note operation dates) opt out in their own configuration.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();

        configurationBuilder.Properties<DateTime?>()
            .HaveConversion<UtcNullableDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MizanDbContext).Assembly);
    }
}
