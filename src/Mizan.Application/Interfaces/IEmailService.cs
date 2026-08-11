namespace Mizan.Application.Interfaces;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string otpCode, string recipientName = "", CancellationToken cancellationToken = default);
}
