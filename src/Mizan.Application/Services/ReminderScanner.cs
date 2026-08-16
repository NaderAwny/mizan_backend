using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.DTOs.Notifications;
using Mizan.Application.Interfaces;
using Mizan.Core.Entities;
using Mizan.Core.Enums;
using Mizan.Core.Interfaces;

namespace Mizan.Application.Services;

public class ReminderScanner : IReminderScanner
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IOptions<RemindersOptions> _options;
    private readonly ILogger<ReminderScanner> _logger;

    public ReminderScanner(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IOptions<RemindersOptions> options,
        ILogger<ReminderScanner> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _options = options;
        _logger = logger;
    }

    public async Task<int> ScanAndProcessRemindersAsync(DateTime? referenceDate = null, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation("ℹ️ Reminders feature is disabled in configuration.");
            return 0;
        }

        var today = (referenceDate ?? DateTime.UtcNow).Date;

        // Pre-due stages from config + mandatory 0 (due date itself)
        var stages = (options.DaysBeforeDue ?? new List<int>())
            .Concat(new[] { 0 })
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        int sentCount = 0;

        foreach (var daysBeforeDue in stages)
        {
            var targetDueDate = today.AddDays(daysBeforeDue);

            var installments = await _unitOfWork.Installments.GetPendingByDueDateAsync(targetDueDate, cancellationToken);

            foreach (var installment in installments)
            {
                if (installment.Status != InstallmentStatus.Pending)
                    continue;

                if (installment.Transaction == null || !installment.Transaction.IsActive)
                    continue;

                // 1. Check if reminder for this installment and stage has already been logged
                bool alreadyLogged = await _unitOfWork.InstallmentReminderLogs.ExistsAsync(installment.Id, daysBeforeDue, cancellationToken);
                if (alreadyLogged)
                    continue;

                var transaction = installment.Transaction;
                var owner = transaction.Owner;
                var contact = transaction.Contact;

                if (owner == null)
                {
                    owner = await _unitOfWork.Users.GetByIdAsync(transaction.OwnerUserId, cancellationToken);
                }

                if (owner == null || !owner.IsActive)
                    continue;

                string contactName = contact?.Name ?? "طرف غير محدد";
                string recipientName = $"{owner.FirstName} {owner.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(recipientName))
                    recipientName = owner.Email;

                // 2. Send email reminder
                bool emailSent = await _emailService.SendInstallmentReminderEmailAsync(
                    owner.Email,
                    recipientName,
                    contactName,
                    installment.Amount,
                    installment.DueDate,
                    daysBeforeDue,
                    cancellationToken);

                if (!emailSent)
                {
                    _logger.LogWarning("⚠️ Email delivery failed for installment {InstallmentId} at stage {DaysBeforeDue} days. Reminder will be retried on next scan.",
                        installment.Id, daysBeforeDue);
                    continue;
                }

                // 3. Create in-app Notification
                var notification = Notification.CreateInstallmentReminder(
                    transaction.OwnerUserId,
                    transaction.Id,
                    installment.Id,
                    contactName,
                    installment.Amount,
                    installment.DueDate,
                    daysBeforeDue);

                await _unitOfWork.Notifications.AddAsync(notification, cancellationToken);

                // 4. Create InstallmentReminderLog row
                var reminderLog = InstallmentReminderLog.Create(installment.Id, daysBeforeDue);
                await _unitOfWork.InstallmentReminderLogs.AddAsync(reminderLog, cancellationToken);

                // 5. Commit both Notification and Log
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                sentCount++;

                _logger.LogInformation("🔔 Processed reminder for installment {InstallmentId} (owner: {OwnerId}, stage: {DaysBeforeDue} days)",
                    installment.Id, transaction.OwnerUserId, daysBeforeDue);
            }
        }

        return sentCount;
    }
}
