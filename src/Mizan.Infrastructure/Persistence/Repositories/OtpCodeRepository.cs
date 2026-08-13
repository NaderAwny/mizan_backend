using Microsoft.EntityFrameworkCore;
using Mizan.Core.Entities;
using Mizan.Core.Interfaces;

namespace Mizan.Infrastructure.Persistence.Repositories;

public class OtpCodeRepository : BaseRepository<OtpCode>, IOtpCodeRepository
{
    public OtpCodeRepository(MizanDbContext context) : base(context)
    {
    }

    public async Task<OtpCode?> GetLatestValidOtpAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        return await _dbSet
            .Where(o => o.Email == normalizedEmail && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task InvalidatePreviousOtpsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        var validOtps = await _dbSet
            .Where(o => o.Email == normalizedEmail && !o.IsUsed)
            .ToListAsync(cancellationToken);

        foreach (var otp in validOtps)
        {
            otp.MarkAsUsed();
        }
    }
}
