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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MizanDbContext).Assembly);
    }
}
