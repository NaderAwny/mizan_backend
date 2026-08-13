namespace Mizan.Core.Interfaces;

public interface IOtpCodeRepository : IBaseRepository<Core.Entities.OtpCode>
{
    Task<Core.Entities.OtpCode?> GetLatestValidOtpAsync(string email, CancellationToken cancellationToken = default);
    Task InvalidatePreviousOtpsAsync(string email, CancellationToken cancellationToken = default);
}
