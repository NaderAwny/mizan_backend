using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;
using Mizan.Core.Exceptions;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Mizan.Infrastructure.Services.Email;

public class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ISendGridClient _sendGridClient;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailOptions> options,
        ILogger<EmailService> logger,
        ISendGridClient? sendGridClient = null)
    {
        _options = options.Value;
        _logger = logger;
        _sendGridClient = sendGridClient ?? new SendGridClient(_options.ApiKey ?? string.Empty);
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

        // In development / mock mode: log without sending
        if (_options.UseMockInDevelopment)
        {
            _logger.LogInformation("📧 [DEV MOCK EMAIL] OTP for {Email} is: {OtpCode}", toEmail, otpCode);
            return true;
        }

        try
        {
            var from = new EmailAddress(_options.SenderEmail, _options.SenderName);
            var to = new EmailAddress(toEmail.Trim());
            const string subject = "كود التحقق لتطبيق ميزان";

            var plainTextContent = $"مرحباً بك في تطبيق ميزان،\n\nكود التحقق الخاص بك هو:\n{otpCode}\n\nهذا الكود صالح لمدة دقيقتين فقط.\nيرجى عدم مشاركة هذا الكود مع أي شخص.\n\nتطبيق ميزان";

            var htmlContent = $@"<!DOCTYPE html>
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
</html>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
            var response = await _sendGridClient.SendEmailAsync(msg, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ OTP email sent successfully via SendGrid to {Email} (StatusCode: {StatusCode})", toEmail, response.StatusCode);
                return true;
            }

            string responseBody = string.Empty;
            if (response.Body != null)
            {
                responseBody = await response.Body.ReadAsStringAsync();
            }

            _logger.LogWarning("⚠️ SendGrid returned non-success status code {StatusCode} when sending OTP to {Email}. Response body: {ResponseBody}",
                response.StatusCode, toEmail, responseBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Transient error sending OTP email via SendGrid to {Email}", toEmail);
            return false;
        }
    }

    public async Task<bool> SendInstallmentReminderEmailAsync(
        string toEmail,
        string recipientName,
        string contactName,
        decimal amount,
        DateTime dueDate,
        int daysUntilDue,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("⚠️ Cannot send installment reminder email: recipient email is empty.");
            return false;
        }

        string formattedAmount = amount.ToString("N2");
        string dueText = daysUntilDue switch
        {
            0 => "مستحق اليوم",
            1 => "مستحق غداً (خلال يوم واحد)",
            2 => "مستحق خلال يومين",
            _ => $"مستحق خلال {daysUntilDue} أيام"
        };

        // In development / mock mode: log without sending
        if (_options.UseMockInDevelopment)
        {
            _logger.LogInformation(
                "📧 [DEV MOCK EMAIL] Installment reminder for {Email} ({Recipient}) | Contact: {Contact} | Amount: {Amount} | DueDate: {DueDate:yyyy-MM-dd} | Status: {DueText}",
                toEmail, recipientName, contactName, formattedAmount, dueDate, dueText);
            return true;
        }

        try
        {
            var from = new EmailAddress(_options.SenderEmail, _options.SenderName);
            var to = new EmailAddress(toEmail.Trim());
            string subject = daysUntilDue == 0
                ? $"تذكير: قسط مستحق اليوم بقيمة {formattedAmount} — تطبيق ميزان"
                : $"تذكير: موعد استحقاق قسط بقيمة {formattedAmount} — تطبيق ميزان";

            var plainTextContent = $"مرحباً {recipientName}،\n\nنود تذكيرك بأن هناك قسطاً مسجلاً في حسابك بتطبيق ميزان:\n- الطرف: {contactName}\n- المبلغ: {formattedAmount}\n- موعد الاستحقاق: {dueDate:yyyy-MM-dd} ({dueText})\n\nتطبيق ميزان";

            var htmlContent = $@"<!DOCTYPE html>
<html dir=""rtl"" lang=""ar"">
<head>
    <meta charset=""UTF-8"">
</head>
<body style=""font-family: Arial, sans-serif; background-color: #f9f9f9; padding: 20px; text-align: right; direction: rtl;"">
    <div style=""max-width: 480px; margin: 0 auto; background: #ffffff; border: 1px solid #e0e0e0; border-radius: 8px; padding: 24px;"">
        <h2 style=""color: #10b981; text-align: center; margin-bottom: 20px;"">تطبيق ميزان — Mizan</h2>
        <p style=""font-size: 16px; color: #333333;"">مرحباً {recipientName}،</p>
        <p style=""font-size: 15px; color: #555555;"">نود تذكيرك بموعد استحقاق القسط التالي:</p>
        <div style=""background-color: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 8px; padding: 16px; margin: 20px 0;"">
            <p style=""margin: 6px 0; font-size: 15px; color: #1f2937;""><strong>الطرف:</strong> {contactName}</p>
            <p style=""margin: 6px 0; font-size: 15px; color: #1f2937;""><strong>المبلغ:</strong> <span style=""color: #059669; font-weight: bold; font-size: 18px;"">{formattedAmount}</span></p>
            <p style=""margin: 6px 0; font-size: 15px; color: #1f2937;""><strong>تاريخ الاستحقاق:</strong> {dueDate:yyyy-MM-dd}</p>
            <p style=""margin: 6px 0; font-size: 14px; color: #d97706; font-weight: bold;"">⚠️ {dueText}</p>
        </div>
        <p style=""font-size: 13px; color: #888888;"">يمكنك مراجعة تفاصيل العملية وسداد القسط مباشرة عبر تطبيق ميزان.</p>
        <hr style=""border: none; border-top: 1px solid #eeeeee; margin: 20px 0;"" />
        <p style=""font-size: 12px; color: #aaaaaa; text-align: center;"">&copy; {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.</p>
    </div>
</body>
</html>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
            var response = await _sendGridClient.SendEmailAsync(msg, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Installment reminder email sent successfully via SendGrid to {Email} (StatusCode: {StatusCode})", toEmail, response.StatusCode);
                return true;
            }

            string responseBody = string.Empty;
            if (response.Body != null)
            {
                responseBody = await response.Body.ReadAsStringAsync();
            }

            _logger.LogWarning("⚠️ SendGrid returned non-success status code {StatusCode} when sending reminder to {Email}. Response body: {ResponseBody}",
                response.StatusCode, toEmail, responseBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Transient error sending installment reminder email via SendGrid to {Email}", toEmail);
            return false;
        }
    }
}
