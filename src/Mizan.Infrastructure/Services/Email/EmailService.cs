using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Mizan.Application.Interfaces;
using Mizan.Core.Exceptions;

namespace Mizan.Infrastructure.Services.Email;

public class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            throw new BadRequestException("البريد الإلكتروني مطلوب");
        }

        if (string.IsNullOrWhiteSpace(otpCode))
        {
            throw new BadRequestException("كود التحقق مطلوب");
        }

        // In development / mock mode when credentials are not configured or UseMockInDevelopment is true
        if (_options.UseMockInDevelopment && (string.IsNullOrWhiteSpace(_options.SenderEmail) || string.IsNullOrWhiteSpace(_options.SenderPassword)))
        {
            _logger.LogInformation("📧 [DEV MOCK EMAIL] OTP for {Email} is: {OtpCode}", toEmail, otpCode);
            return true;
        }

        try
        {
            var message = new MimeMessage();
            var fromEmail = _options.SenderEmail;
            message.From.Add(new MailboxAddress("تطبيق ميزان", fromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail.Trim()));
            message.Subject = "كود التحقق لتطبيق ميزان";
            message.Date = DateTimeOffset.Now;

            var bodyBuilder = new BodyBuilder
            {
                TextBody = $"مرحباً بك في تطبيق ميزان،\n\nكود التحقق الخاص بك هو:\n{otpCode}\n\nهذا الكود صالح لمدة دقيقتين فقط.\nيرجى عدم مشاركة هذا الكود مع أي شخص.\n\nتطبيق ميزان",
                HtmlBody = $@"
<!DOCTYPE html>
<html dir=""rtl"" lang=""ar"">
<head>
    <meta charset=""UTF-8"">
</head>
<body style=""font-family: Arial, sans-serif; background-color: #f9f9f9; padding: 20px; text-align: right; direction: rtl;"">
    <div style=""max-width: 480px; margin: 0 auto; background: #ffffff; border: 1px solid #e0e0e0; border-radius: 8px; padding: 24px;"">
        <h2 style=""color: #10b981; text-align: center; margin-bottom: 20px;"">تطبيق ميزان — Mizan</h2>
        <p style=""font-size: 16px; color: #333333;"">مرحباً بك،</p>
        <p style=""font-size: 15px; color: #555555;"">استخدم كود التحقق التالي لتسجيل الدخول إلى حسابك:</p>
        <div style=""background-color: #f0fdf4; border: 2px dashed #10b981; border-radius: 8px; padding: 14px; text-align: center; font-size: 28px; font-weight: bold; letter-spacing: 4px; color: #065f46; margin: 20px 0;"">
            {otpCode}
        </div>
        <p style=""font-size: 13px; color: #888888;"">⚠️ هذا الكود صالح لمدة <strong>دقيقتين فقط</strong>. برجاء عدم مشاركته مع أي شخص.</p>
        <hr style=""border: none; border-top: 1px solid #eeeeee; margin: 20px 0;"" />
        <p style=""font-size: 12px; color: #aaaaaa; text-align: center;"">&copy; {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.</p>
    </div>
</body>
</html>"
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            client.CheckCertificateRevocation = false;
            client.Timeout = 10000;

            var socketOption = _options.SmtpPort == 465 
                ? SecureSocketOptions.SslOnConnect 
                : (_options.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);

            await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, socketOption, cancellationToken);
            
            if (!string.IsNullOrWhiteSpace(_options.SenderEmail) && !string.IsNullOrWhiteSpace(_options.SenderPassword))
            {
                await client.AuthenticateAsync(_options.SenderEmail, _options.SenderPassword, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("✅ OTP email sent successfully to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to send OTP email to {Email}", toEmail);
            throw new BadRequestException("فشل إرسال كود التحقق عبر البريد الإلكتروني، يرجى التأكد من صحة البريد والمحاولة لاحقاً");
        }
    }
}
