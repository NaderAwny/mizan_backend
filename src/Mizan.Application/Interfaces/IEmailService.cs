namespace Mizan.Application.Interfaces;

public interface IEmailService
{
    Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default);
    Task<bool> SendInstallmentReminderEmailAsync(string toEmail, string recipientName, string contactName, decimal amount, DateTime dueDate, int daysUntilDue, CancellationToken cancellationToken = default);
}
