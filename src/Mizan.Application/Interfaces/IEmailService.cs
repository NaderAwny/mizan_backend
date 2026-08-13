namespace Mizan.Application.Interfaces;

public interface IEmailService
{
    Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default);
}
