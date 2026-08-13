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
            message.From.Add(new MailboxAddress(_options.FromName, fromEmail));
            message.To.Add(new MailboxAddress(toEmail.Trim(), toEmail.Trim()));
            message.Subject = $"🔐 كود التحقق لتطبيق ميزان: {otpCode}";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
<!DOCTYPE html>
<html dir=""rtl"" lang=""ar"">
<head>
    <meta charset=""UTF-8"">
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f7f6; margin: 0; padding: 20px; text-align: right; }}
        .container {{ max-width: 500px; margin: 0 auto; background: #ffffff; border-radius: 12px; padding: 30px; box-shadow: 0 4px 15px rgba(0,0,0,0.05); }}
        .header {{ text-align: center; margin-bottom: 25px; }}
        .logo {{ font-size: 28px; font-weight: bold; color: #10B981; }}
        .content {{ color: #374151; font-size: 16px; line-height: 1.6; text-align: right; }}
        .otp-box {{ background: #ECFDF5; border: 2px dashed #10B981; border-radius: 10px; padding: 18px; text-align: center; font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #065F46; margin: 25px 0; }}
        .footer {{ text-align: center; color: #9CA3AF; font-size: 13px; margin-top: 25px; border-top: 1px solid #E5E7EB; padding-top: 15px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <div class=""logo"">ميزان — Mizan</div>
        </div>
        <div class=""content"">
            <p>مرحباً بك،</p>
            <p>استخدم كود التحقق التالي لتسجيل الدخول إلى حسابك في تطبيق ميزان:</p>
            <div class=""otp-box"">{otpCode}</div>
            <p>⚠️ هذا الكود صالح لمدة <strong>دقيقتين فقط</strong>. برجاء عدم مشاركة الكود مع أي شخص.</p>
        </div>
        <div class=""footer"">
            <p>إذا لم تطلب هذا الكود، يمكنك تجاهل هذه الرسالة بأمان.</p>
            <p>&copy; {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.</p>
        </div>
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
