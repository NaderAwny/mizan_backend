namespace Mizan.Application.Interfaces;

public interface IWhatsAppService
{
    Task<bool> SendOtpMessageAsync(string toWhatsAppNumber, string otpCode, CancellationToken cancellationToken = default);
    Task<bool> SendTextMessageAsync(string toWhatsAppNumber, string message, CancellationToken cancellationToken = default);
}
