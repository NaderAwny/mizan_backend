using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.Interfaces;
using Mizan.Core.Exceptions;

namespace Mizan.Infrastructure.Services.Email;

public class EmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmailService> _logger;

    private const string BrevoApiUrl = "https://api.brevo.com/v3/smtp/email";

    public EmailService(
        IOptions<EmailOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<EmailService> logger)
    {
        _options = options.Value;
        _httpClient = httpClientFactory.CreateClient("Brevo");
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    //  Helper: Send a single email via Brevo API
    // ─────────────────────────────────────────────────────────────────
    private async Task<bool> SendViaBrevoAsync(
        string toEmail,
        string subject,
        string htmlContent,
        string plainTextContent,
        byte[]? attachmentBytes = null,
        string? attachmentName = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object>
        {
            ["sender"]  = new { name = _options.SenderName, email = _options.SenderEmail },
            ["to"]      = new[] { new { email = toEmail.Trim() } },
            ["subject"] = subject,
            ["htmlContent"]  = htmlContent,
            ["textContent"]  = plainTextContent,
            ["replyTo"]      = new { email = _options.SenderEmail, name = _options.SenderName }
        };

        if (attachmentBytes != null && attachmentBytes.Length > 0 && !string.IsNullOrWhiteSpace(attachmentName))
        {
            body["attachment"] = new[]
            {
                new
                {
                    content = Convert.ToBase64String(attachmentBytes),
                    name    = attachmentName
                }
            };
        }

        var json    = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(BrevoApiUrl, content, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "⚠️ Brevo returned non-success status code {StatusCode} when sending email to {Email}. Body: {Body}",
            response.StatusCode, toEmail, responseBody);
        return false;
    }

    // ─────────────────────────────────────────────────────────────────
    //  OTP Email
    // ─────────────────────────────────────────────────────────────────
    public async Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new BadRequestException("البريد الإلكتروني مطلوب");

        if (string.IsNullOrWhiteSpace(otpCode))
            throw new BadRequestException("كود التحقق مطلوب");

        if (_options.UseMockInDevelopment)
        {
            _logger.LogInformation("📧 [DEV MOCK EMAIL] OTP for {Email} is: {OtpCode}", toEmail, otpCode);
            return true;
        }

        try
        {
            const string subject = "كود التحقق لتطبيق ميزان";

            var plainText = $@"مرحباً بك في تطبيق ميزان،

كود التحقق الخاص بك لتسجيل الدخول هو:
{otpCode}

⚠️ هذا الكود صالح لمدة دقيقتين فقط. يرجى عدم مشاركة هذا الكود مع أي شخص لحماية أمان حسابك.

---
هذه رسالة تلقائية من تطبيق ميزان. إذا كنت لا تتوقع هذه الرسالة، يمكنك تجاهلها بأمان.
تطبيق ميزان — الزقازيق، الشرقية، مصر
© {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.";

            var html = $@"<!DOCTYPE html>
<html dir=""rtl"" lang=""ar"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>كود التحقق لتطبيق ميزان</title>
</head>
<body style=""margin: 0; padding: 20px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f3f4f6; text-align: right; direction: rtl; color: #1f2937; line-height: 1.6;"">
    <div style=""max-width: 500px; margin: 0 auto; background-color: #ffffff; border: 1px solid #e5e7eb; border-radius: 10px; padding: 28px; box-shadow: 0 1px 3px rgba(0,0,0,0.05);"">
        <h2 style=""color: #059669; text-align: center; margin-top: 0; margin-bottom: 20px; font-size: 22px;"">تطبيق ميزان — Mizan</h2>
        <p style=""font-size: 16px; margin: 0 0 12px 0;"">مرحباً بك،</p>
        <p style=""font-size: 15px; color: #4b5563; margin: 0 0 20px 0;"">استخدم كود التحقق التالي لتسجيل الدخول إلى حسابك:</p>
        <div style=""background-color: #ecfdf5; border: 2px dashed #059669; border-radius: 8px; padding: 16px; text-align: center; font-size: 30px; font-weight: bold; letter-spacing: 6px; color: #065f46; margin: 20px 0;"">
            {otpCode}
        </div>
        <p style=""font-size: 13px; color: #6b7280; margin: 0 0 20px 0;"">⚠️ هذا الكود صالح لمدة <strong>دقيقتين فقط</strong>. يرجى عدم مشاركة هذا الكود مع أي شخص لحماية أمان حسابك.</p>
        <hr style=""border: none; border-top: 1px solid #e5e7eb; margin: 20px 0;"" />
        <div style=""font-size: 12px; color: #9ca3af; text-align: center; line-height: 1.6;"">
            <p style=""margin: 4px 0;"">هذه رسالة تلقائية من تطبيق ميزان. إذا كنت لا تتوقع هذه الرسالة، يمكنك تجاهلها بأمان.</p>
            <p style=""margin: 4px 0;"">تطبيق ميزان — الزقازيق، الشرقية، مصر</p>
            <p style=""margin: 4px 0;"">© {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.</p>
        </div>
    </div>
</body>
</html>";

            var result = await SendViaBrevoAsync(toEmail, subject, html, plainText, cancellationToken: cancellationToken);
            if (result)
                _logger.LogInformation("✅ OTP email sent successfully via Brevo to {Email}", toEmail);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Transient error sending OTP email via Brevo to {Email}", toEmail);
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Installment Reminder (to shop owner)
    // ─────────────────────────────────────────────────────────────────
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

        if (_options.UseMockInDevelopment)
        {
            _logger.LogInformation(
                "📧 [DEV MOCK EMAIL] Installment reminder for {Email} ({Recipient}) | Contact: {Contact} | Amount: {Amount} | DueDate: {DueDate:yyyy-MM-dd} | Status: {DueText}",
                toEmail, recipientName, contactName, formattedAmount, dueDate, dueText);
            return true;
        }

        try
        {
            string subject = daysUntilDue == 0
                ? $"تذكير: قسط مستحق اليوم بقيمة {formattedAmount} — تطبيق ميزان"
                : $"تذكير: موعد استحقاق قسط بقيمة {formattedAmount} — تطبيق ميزان";

            var plainText = $@"مرحباً {recipientName}،

نود تذكيرك بأن هناك قسطاً مسجلاً في حسابك بتطبيق ميزان:
- الطرف: {contactName}
- المبلغ المستحق: {formattedAmount}
- موعد الاستحقاق: {dueDate:yyyy-MM-dd} ({dueText})

يمكنك مراجعة تفاصيل العملية وسداد القسط مباشرة عبر تطبيق ميزان.

---
هذه رسالة تلقائية من تطبيق ميزان. إذا كنت لا تتوقع هذه الرسالة، يمكنك تجاهلها بأمان.
تطبيق ميزان — الزقازيق، الشرقية، مصر
© {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.";

            var html = $@"<!DOCTYPE html>
<html dir=""rtl"" lang=""ar"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>تذكير بموعد استحقاق القسط — تطبيق ميزان</title>
</head>
<body style=""margin: 0; padding: 20px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f3f4f6; text-align: right; direction: rtl; color: #1f2937; line-height: 1.6;"">
    <div style=""max-width: 500px; margin: 0 auto; background-color: #ffffff; border: 1px solid #e5e7eb; border-radius: 10px; padding: 28px; box-shadow: 0 1px 3px rgba(0,0,0,0.05);"">
        <h2 style=""color: #059669; text-align: center; margin-top: 0; margin-bottom: 20px; font-size: 22px;"">تطبيق ميزان — Mizan</h2>
        <p style=""font-size: 16px; margin: 0 0 12px 0;"">مرحباً {recipientName}،</p>
        <p style=""font-size: 15px; color: #4b5563; margin: 0 0 20px 0;"">نود تذكيرك بموعد استحقاق القسط التالي المسجل في حسابك:</p>
        <div style=""background-color: #f9fafb; border: 1px solid #e5e7eb; border-radius: 8px; padding: 18px; margin: 20px 0;"">
            <p style=""margin: 6px 0; font-size: 15px; color: #374151;""><strong>الطرف:</strong> {contactName}</p>
            <p style=""margin: 6px 0; font-size: 15px; color: #374151;""><strong>المبلغ المستحق:</strong> <span style=""color: #059669; font-weight: bold; font-size: 18px;"">{formattedAmount}</span></p>
            <p style=""margin: 6px 0; font-size: 15px; color: #374151;""><strong>تاريخ الاستحقاق:</strong> {dueDate:yyyy-MM-dd}</p>
            <p style=""margin: 6px 0; font-size: 14px; color: #d97706; font-weight: bold;"">⚠️ {dueText}</p>
        </div>
        <p style=""font-size: 14px; color: #4b5563; margin: 0 0 20px 0;"">يمكنك مراجعة تفاصيل العملية وسداد القسط مباشرة عبر تطبيق ميزان.</p>
        <hr style=""border: none; border-top: 1px solid #e5e7eb; margin: 20px 0;"" />
        <div style=""font-size: 12px; color: #9ca3af; text-align: center; line-height: 1.6;"">
            <p style=""margin: 4px 0;"">هذه رسالة تلقائية من تطبيق ميزان. إذا كنت لا تتوقع هذه الرسالة، يمكنك تجاهلها بأمان.</p>
            <p style=""margin: 4px 0;"">تطبيق ميزان — الزقازيق، الشرقية، مصر</p>
            <p style=""margin: 4px 0;"">© {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.</p>
        </div>
    </div>
</body>
</html>";

            var result = await SendViaBrevoAsync(toEmail, subject, html, plainText, cancellationToken: cancellationToken);
            if (result)
                _logger.LogInformation("✅ Installment reminder email sent successfully via Brevo to {Email}", toEmail);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Transient error sending installment reminder email via Brevo to {Email}", toEmail);
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Installment Reminder (to contact / debtor)
    // ─────────────────────────────────────────────────────────────────
    public async Task<bool> SendInstallmentReminderToContactEmailAsync(
        string toEmail,
        string contactName,
        string shopOwnerName,
        decimal amount,
        DateTime dueDate,
        int daysUntilDue,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("⚠️ Cannot send installment reminder email to contact: email is empty.");
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

        if (_options.UseMockInDevelopment)
        {
            _logger.LogInformation(
                "📧 [DEV MOCK EMAIL] Contact Installment reminder for {Email} ({Contact}) | Shop Owner: {ShopOwner} | Amount: {Amount} | DueDate: {DueDate:yyyy-MM-dd} | Status: {DueText}",
                toEmail, contactName, shopOwnerName, formattedAmount, dueDate, dueText);
            return true;
        }

        try
        {
            string subject = daysUntilDue == 0
                ? $"تذكير: موعد سداد قسط مستحق اليوم لصالح {shopOwnerName} — تطبيق ميزان"
                : $"تذكير: موعد سداد قسط مستحق قريباً لصالح {shopOwnerName} — تطبيق ميزان";

            var plainText = $@"مرحباً {contactName}،

نود تذكيرك بأن هناك قسطاً مستحقاً عليك لصالح {shopOwnerName}:
- المبلغ المستحق: {formattedAmount} جنيه
- موعد الاستحقاق: {dueDate:yyyy-MM-dd} ({dueText})

يرجى التنسيق مع {shopOwnerName} لإتمام عملية السداد في الموعد المحدد.

---
هذه رسالة تلقائية من تطبيق ميزان. إذا كنت لا تتوقع هذه الرسالة، يمكنك تجاهلها بأمان.
تطبيق ميزان — الزقازيق، الشرقية، مصر
© {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.";

            var html = $@"<!DOCTYPE html>
<html dir=""rtl"" lang=""ar"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>تذكير بموعد سداد القسط — تطبيق ميزان</title>
</head>
<body style=""margin: 0; padding: 20px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f3f4f6; text-align: right; direction: rtl; color: #1f2937; line-height: 1.6;"">
    <div style=""max-width: 500px; margin: 0 auto; background-color: #ffffff; border: 1px solid #e5e7eb; border-radius: 10px; padding: 28px; box-shadow: 0 1px 3px rgba(0,0,0,0.05);"">
        <h2 style=""color: #059669; text-align: center; margin-top: 0; margin-bottom: 20px; font-size: 22px;"">تطبيق ميزان — Mizan</h2>
        <p style=""font-size: 16px; margin: 0 0 12px 0;"">مرحباً {contactName}،</p>
        <p style=""font-size: 15px; color: #4b5563; margin: 0 0 20px 0;"">نود تذكيرك بموعد استحقاق القسط التالي المسجل لصالح <strong>{shopOwnerName}</strong>:</p>
        <div style=""background-color: #f9fafb; border: 1px solid #e5e7eb; border-radius: 8px; padding: 18px; margin: 20px 0;"">
            <p style=""margin: 6px 0; font-size: 15px; color: #374151;""><strong>المستفيد / صاحب المحل:</strong> {shopOwnerName}</p>
            <p style=""margin: 6px 0; font-size: 15px; color: #374151;""><strong>المبلغ المستحق:</strong> <span style=""color: #059669; font-weight: bold; font-size: 18px;"">{formattedAmount} جنيه</span></p>
            <p style=""margin: 6px 0; font-size: 15px; color: #374151;""><strong>تاريخ الاستحقاق:</strong> {dueDate:yyyy-MM-dd}</p>
            <p style=""margin: 6px 0; font-size: 14px; color: #d97706; font-weight: bold;"">⚠️ {dueText}</p>
        </div>
        <p style=""font-size: 14px; color: #4b5563; margin: 0 0 20px 0;"">يرجى التنسيق مع <strong>{shopOwnerName}</strong> لإتمام عملية السداد.</p>
        <hr style=""border: none; border-top: 1px solid #e5e7eb; margin: 20px 0;"" />
        <div style=""font-size: 12px; color: #9ca3af; text-align: center; line-height: 1.6;"">
            <p style=""margin: 4px 0;"">هذه رسالة تلقائية من تطبيق ميزان. إذا كنت لا تتوقع هذه الرسالة، يمكنك تجاهلها بأمان.</p>
            <p style=""margin: 4px 0;"">تطبيق ميزان — الزقازيق، الشرقية، مصر</p>
            <p style=""margin: 4px 0;"">© {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.</p>
        </div>
    </div>
</body>
</html>";

            var result = await SendViaBrevoAsync(toEmail, subject, html, plainText, cancellationToken: cancellationToken);
            if (result)
                _logger.LogInformation("✅ Contact installment reminder email sent successfully via Brevo to {Email}", toEmail);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Transient error sending contact installment reminder email via Brevo to {Email}", toEmail);
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Periodic Report Email (with PDF attachment)
    // ─────────────────────────────────────────────────────────────────
    public async Task<bool> SendPeriodicReportEmailAsync(
        string toEmail,
        string recipientName,
        int batchNumber,
        byte[] pdfBytes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("⚠️ Cannot send periodic report email: recipient email is empty.");
            return false;
        }

        if (_options.UseMockInDevelopment)
        {
            _logger.LogInformation(
                "📧 [DEV MOCK EMAIL] Periodic Report for {Email} ({Recipient}) | Batch: #{BatchNumber} | PDF Size: {Size} bytes",
                toEmail, recipientName, batchNumber, pdfBytes?.Length ?? 0);
            return true;
        }

        try
        {
            string subject = $"التقرير الدوري للعمليات #{batchNumber} — تطبيق ميزان";

            var plainText = $@"مرحباً {recipientName}،

يسعدنا إعلامك بأنه تم إصدار التقرير الدوري للعمليات الخاص بحسابك (الدفعة #{batchNumber}).

تجد مرفقاً مع هذه الرسالة ملف PDF يحتوي على ملخص شامل وتفاصيل العمليات الـ 7 الأخيرة.

---
هذه رسالة تلقائية من تطبيق ميزان.
تطبيق ميزان — الزقازيق، الشرقية، مصر
© {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.";

            var html = $@"<!DOCTYPE html>
<html dir=""rtl"" lang=""ar"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>التقرير الدوري للعمليات — تطبيق ميزان</title>
</head>
<body style=""margin: 0; padding: 20px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f3f4f6; text-align: right; direction: rtl; color: #1f2937; line-height: 1.6;"">
    <div style=""max-width: 500px; margin: 0 auto; background-color: #ffffff; border: 1px solid #e5e7eb; border-radius: 10px; padding: 28px; box-shadow: 0 1px 3px rgba(0,0,0,0.05);"">
        <h2 style=""color: #059669; text-align: center; margin-top: 0; margin-bottom: 20px; font-size: 22px;"">تطبيق ميزان — Mizan</h2>
        <p style=""font-size: 16px; margin: 0 0 12px 0;"">مرحباً {recipientName}،</p>
        <p style=""font-size: 15px; color: #4b5563; margin: 0 0 20px 0;"">تم إصدار التقرير الدوري للعمليات المسجلة في حسابك بنجاح للدفعة <strong>#{batchNumber}</strong>.</p>
        <div style=""background-color: #ecfdf5; border: 1px solid #a7f3d0; border-radius: 8px; padding: 16px; text-align: center; margin: 20px 0;"">
            <p style=""margin: 0; font-size: 15px; color: #065f46; font-weight: bold;"">📄 تم إرفاق ملف التقرير (PDF) بهذه الرسالة</p>
            <p style=""margin: 6px 0 0 0; font-size: 13px; color: #047857;"">يتضمن ملخص المبيعات والمشتريات وجدول تفصيلي بالعمليات السبع الأخيرة.</p>
        </div>
        <p style=""font-size: 14px; color: #4b5563; margin: 0 0 20px 0;"">يمكنك أيضاً استعراض وتحميل كافة تقاريرك الدورية السابقة في أي وقت من خلال تطبيق ميزان.</p>
        <hr style=""border: none; border-top: 1px solid #e5e7eb; margin: 20px 0;"" />
        <div style=""font-size: 12px; color: #9ca3af; text-align: center; line-height: 1.6;"">
            <p style=""margin: 4px 0;"">هذه رسالة تلقائية من تطبيق ميزان. إذا كنت لا تتوقع هذه الرسالة، يمكنك تجاهلها بأمان.</p>
            <p style=""margin: 4px 0;"">تطبيق ميزان — الزقازيق، الشرقية، مصر</p>
            <p style=""margin: 4px 0;"">© {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.</p>
        </div>
    </div>
</body>
</html>";

            var result = await SendViaBrevoAsync(
                toEmail, subject, html, plainText,
                pdfBytes, $"mizan-report-batch-{batchNumber}.pdf",
                cancellationToken);

            if (result)
                _logger.LogInformation("✅ Periodic report #{BatchNumber} email sent successfully via Brevo to {Email}", batchNumber, toEmail);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Transient error sending periodic report email via Brevo to {Email}", toEmail);
            return false;
        }
    }
}
