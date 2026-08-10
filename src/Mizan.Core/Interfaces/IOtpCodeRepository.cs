using Mizan.Core.Entities;

namespace Mizan.Core.Interfaces;

public interface IOtpCodeRepository : IBaseRepository<OtpCode>
{
    Task<OtpCode?> GetLatestValidOtpAsync(string whatsappNumber, CancellationToken cancellationToken = default);
    Task InvalidatePreviousOtpsAsync(string whatsappNumber, CancellationToken cancellationToken = default);
}
