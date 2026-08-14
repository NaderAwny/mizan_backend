using Microsoft.EntityFrameworkCore;
using Mizan.Core.Entities;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MizanDbContext).Assembly);
    }
}
