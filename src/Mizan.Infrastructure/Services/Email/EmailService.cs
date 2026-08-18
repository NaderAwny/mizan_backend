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

            var plainTextContent = $@"مرحباً بك في تطبيق ميزان،

كود التحقق الخاص بك لتسجيل الدخول هو:
{otpCode}

⚠️ هذا الكود صالح لمدة دقيقتين فقط. يرجى عدم مشاركة هذا الكود مع أي شخص لحماية أمان حسابك.

---
هذه رسالة تلقائية من تطبيق ميزان. إذا كنت لا تتوقع هذه الرسالة، يمكنك تجاهلها بأمان.
تطبيق ميزان — الزقازيق، الشرقية، مصر
© {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.";

            var htmlContent = $@"<!DOCTYPE html>
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
            <p style=""margin: 4px 0;"">&copy; {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.</p>
        </div>
    </div>
</body>
</html>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
            msg.SetReplyTo(new EmailAddress(_options.SenderEmail, _options.SenderName));
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

            var plainTextContent = $@"مرحباً {recipientName}،

نود تذكيرك بأن هناك قسطاً مسجلاً في حسابك بتطبيق ميزان:
- الطرف: {contactName}
- المبلغ المستحق: {formattedAmount}
- موعد الاستحقاق: {dueDate:yyyy-MM-dd} ({dueText})

يمكنك مراجعة تفاصيل العملية وسداد القسط مباشرة عبر تطبيق ميزان.

---
هذه رسالة تلقائية من تطبيق ميزان. إذا كنت لا تتوقع هذه الرسالة، يمكنك تجاهلها بأمان.
تطبيق ميزان — الزقازيق، الشرقية، مصر
© {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.";

            var htmlContent = $@"<!DOCTYPE html>
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
            <p style=""margin: 4px 0;"">&copy; {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.</p>
        </div>
    </div>
</body>
</html>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
            msg.SetReplyTo(new EmailAddress(_options.SenderEmail, _options.SenderName));
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

        // In development / mock mode: log without sending
        if (_options.UseMockInDevelopment)
        {
            _logger.LogInformation(
                "📧 [DEV MOCK EMAIL] Contact Installment reminder for {Email} ({Contact}) | Shop Owner: {ShopOwner} | Amount: {Amount} | DueDate: {DueDate:yyyy-MM-dd} | Status: {DueText}",
                toEmail, contactName, shopOwnerName, formattedAmount, dueDate, dueText);
            return true;
        }

        try
        {
            var from = new EmailAddress(_options.SenderEmail, _options.SenderName);
            var to = new EmailAddress(toEmail.Trim());
            string subject = daysUntilDue == 0
                ? $"تذكير: موعد سداد قسط مستحق اليوم لصالح {shopOwnerName} — تطبيق ميزان"
                : $"تذكير: موعد سداد قسط مستحق قريباً لصالح {shopOwnerName} — تطبيق ميزان";

            var plainTextContent = $@"مرحباً {contactName}،

نود تذكيرك بأن هناك قسطاً مستحقاً عليك لصالح {shopOwnerName}:
- المبلغ المستحق: {formattedAmount} جنيه
- موعد الاستحقاق: {dueDate:yyyy-MM-dd} ({dueText})

يرجى التنسيق مع {shopOwnerName} لإتمام عملية السداد في الموعد المحدد.

---
هذه رسالة تلقائية من تطبيق ميزان. إذا كنت لا تتوقع هذه الرسالة، يمكنك تجاهلها بأمان.
تطبيق ميزان — الزقازيق، الشرقية، مصر
© {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.";

            var htmlContent = $@"<!DOCTYPE html>
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
            <p style=""margin: 4px 0;"">&copy; {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.</p>
        </div>
    </div>
</body>
</html>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
            msg.SetReplyTo(new EmailAddress(_options.SenderEmail, _options.SenderName));
            var response = await _sendGridClient.SendEmailAsync(msg, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Contact installment reminder email sent successfully via SendGrid to {Email} (StatusCode: {StatusCode})", toEmail, response.StatusCode);
                return true;
            }

            string responseBody = string.Empty;
            if (response.Body != null)
            {
                responseBody = await response.Body.ReadAsStringAsync();
            }

            _logger.LogWarning("⚠️ SendGrid returned non-success status code {StatusCode} when sending reminder to contact {Email}. Response body: {ResponseBody}",
                response.StatusCode, toEmail, responseBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Transient error sending contact installment reminder email via SendGrid to {Email}", toEmail);
            return false;
        }
    }

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

        // In development / mock mode: log without sending
        if (_options.UseMockInDevelopment)
        {
            _logger.LogInformation(
                "📧 [DEV MOCK EMAIL] Periodic Report for {Email} ({Recipient}) | Batch: #{BatchNumber} | PDF Size: {Size} bytes",
                toEmail, recipientName, batchNumber, pdfBytes?.Length ?? 0);
            return true;
        }

        try
        {
            var from = new EmailAddress(_options.SenderEmail, _options.SenderName);
            var to = new EmailAddress(toEmail.Trim());
            string subject = $"التقرير الدوري للعمليات #{batchNumber} — تطبيق ميزان";

            var plainTextContent = $@"مرحباً {recipientName}،

يسعدنا إعلامك بأنه تم إصدار التقرير الدوري للعمليات الخاص بحسابك (الدفعة #{batchNumber}).

تجد مرفقاً مع هذه الرسالة ملف PDF يحتوي على ملخص شامل وتفاصيل العمليات الـ 7 الأخيرة.

---
هذه رسالة تلقائية من تطبيق ميزان.
تطبيق ميزان — الزقازيق، الشرقية، مصر
© {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.";

            var htmlContent = $@"<!DOCTYPE html>
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
            <p style=""margin: 4px 0;"">&copy; {DateTime.UtcNow.Year} تطبيق ميزان. جميع الحقوق محفوظة.</p>
        </div>
    </div>
</body>
</html>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
            msg.SetReplyTo(new EmailAddress(_options.SenderEmail, _options.SenderName));

            if (pdfBytes != null && pdfBytes.Length > 0)
            {
                msg.AddAttachment($"mizan-report-batch-{batchNumber}.pdf", Convert.ToBase64String(pdfBytes), "application/pdf");
            }

            var response = await _sendGridClient.SendEmailAsync(msg, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("✅ Periodic report #{BatchNumber} email sent successfully via SendGrid to {Email} (StatusCode: {StatusCode})", batchNumber, toEmail, response.StatusCode);
                return true;
            }

            string responseBody = string.Empty;
            if (response.Body != null)
            {
                responseBody = await response.Body.ReadAsStringAsync();
            }

            _logger.LogWarning("⚠️ SendGrid returned non-success status code {StatusCode} when sending periodic report to {Email}. Response body: {ResponseBody}",
                response.StatusCode, toEmail, responseBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Transient error sending periodic report email via SendGrid to {Email}", toEmail);
            return false;
        }
    }
}
