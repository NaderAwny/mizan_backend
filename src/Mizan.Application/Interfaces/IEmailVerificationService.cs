using Mizan.Application.DTOs.Auth;

namespace Mizan.Application.Interfaces;

public interface IEmailVerificationService
{
    Task<EmailVerificationResult> VerifyAsync(string email, CancellationToken cancellationToken = default);
}
