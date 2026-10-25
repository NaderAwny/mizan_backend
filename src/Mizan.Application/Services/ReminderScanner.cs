using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mizan.Application.DTOs.Notifications;
using Mizan.Application.Interfaces;
using Mizan.Core.Entities;
using Mizan.Core.Enums;
using Mizan.Core.Interfaces;
using Mizan.Core.Time;

namespace Mizan.Application.Services;

public class ReminderScanner : IReminderScanner
{
    private static readonly SemaphoreSlim ScanLock = new(1, 1);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IPushNotificationService _pushService;
    private readonly IOptions<RemindersOptions> _options;
    private readonly ILogger<ReminderScanner> _logger;

    public ReminderScanner(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IPushNotificationService pushService,
        IOptions<RemindersOptions> options,
        ILogger<ReminderScanner> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _pushService = pushService;
        _options = options;
        _logger = logger;
    }

    public Task<int> ScanAndProcessRemindersAsync(DateTime? referenceDate = null, CancellationToken cancellationToken = default)
    {
        return ScanCoreAsync(null, referenceDate, cancellationToken);
    }

    public Task<int> ScanForOwnerAsync(Guid ownerUserId, DateTime? referenceDate = null, CancellationToken cancellationToken = default)
    {
        return ScanCoreAsync(ownerUserId, referenceDate, cancellationToken);
    }

    private async Task<int> ScanCoreAsync(Guid? ownerUserId, DateTime? referenceDate, CancellationToken ct)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation("ℹ️ Reminders feature is disabled in configuration.");
            return 0;
        }

        await ScanLock.WaitAsync(ct);
        try
        {
            return await ExecuteScanAsync(ownerUserId, referenceDate, options, ct);
        }
        finally
        {
            ScanLock.Release();
        }
    }

    private async Task<int> ExecuteScanAsync(Guid? ownerUserId, DateTime? referenceDate, RemindersOptions options, CancellationToken ct)
    {
        var today = EgyptTime.TodayLocal(referenceDate);

        var stages = (options.DaysBeforeDue ?? new List<int>())
            .Concat(new[] { 0 })
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        int sentCount = 0;
        var maxAttempts = options.MaxChannelAttempts > 0 ? options.MaxChannelAttempts : 3;
        var backoff = TimeSpan.FromMinutes(options.RetryBackoffMinutes > 0 ? options.RetryBackoffMinutes : 5);
        var lookbackDays = options.OverdueLookbackDays > 0 ? options.OverdueLookbackDays : 7;
        var utcNow = DateTime.UtcNow;

        foreach (var stage in stages)
        {
            (DateTime startUtc, DateTime endUtcExclusive) range;
            if (stage == 0)
            {
                // Stage 0 covers overdue within OverdueLookbackDays up through today
                range = EgyptTime.LocalDateRangeToUtc(today.AddDays(-lookbackDays), today);
            }
            else
            {
                range = EgyptTime.LocalDateRangeToUtc(today.AddDays(stage), today.AddDays(stage));
            }

            var installments = await _unitOfWork.Installments.GetPendingByDueRangeAsync(
                range.startUtc, range.endUtcExclusive, ownerUserId, ct);

            if (installments.Count == 0)
                continue;

            var installmentIds = installments.Select(i => i.Id).ToList();
            var logs = await _unitOfWork.InstallmentReminderLogs.GetByInstallmentIdsAsync(installmentIds, stage, ct);

            var workList = new List<(Installment Installment, InstallmentReminderLog Log, Notification? Notification, int DaysUntilDue, bool IsNew)>();

            foreach (var installment in installments)
            {
                if (installment.Status != InstallmentStatus.Pending)
                    continue;

                var transaction = installment.Transaction;
                if (transaction == null || !transaction.IsActive)
                    continue;

                var owner = transaction.Owner;
                if (owner == null || !owner.IsActive)
                    continue;

                var contact = transaction.Contact;
                var contactEmailApplicable = !string.IsNullOrWhiteSpace(contact?.ContactEmail);

                var dueDateLocal = EgyptTime.ToLocalDate(installment.DueDate);
                var daysUntilDue = (dueDateLocal - today).Days; // <= 0 for stage 0, negative = overdue

                logs.TryGetValue(installment.Id, out var log);

                if (log == null)
                {
                    string contactName = contact?.Name ?? "طرف غير محدد";
                    var notification = Notification.CreateInstallmentReminder(
                        transaction.OwnerUserId,
                        transaction.Id,
                        installment.Id,
                        contactName,
                        installment.Amount,
                        installment.DueDate,
                        daysUntilDue);

                    log = InstallmentReminderLog.Create(installment.Id, stage);

                    await _unitOfWork.Notifications.AddAsync(notification, ct);
                    await _unitOfWork.InstallmentReminderLogs.AddAsync(log, ct);

                    workList.Add((installment, log, notification, daysUntilDue, true));
                }
                else
                {
                    if (!log.ShouldRetry(contactEmailApplicable, maxAttempts, backoff, utcNow))
                    {
                        continue;
                    }

                    workList.Add((installment, log, null, daysUntilDue, false));
                }
            }

            // PHASE 1: persists Notifications + logs BEFORE any network call
            try
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "DbUpdateException occurred during Phase 1 save for stage {Stage}. Concurrency race detected; skipping stage.", stage);
                continue;
            }

            // PHASE 2: Execute delivery channels for work items
            foreach (var item in workList)
            {
                var installment = item.Installment;
                var log = item.Log;
                var transaction = installment.Transaction!;
                var owner = transaction.Owner!;
                var contact = transaction.Contact;
                var contactEmailApplicable = !string.IsNullOrWhiteSpace(contact?.ContactEmail);
                var daysUntilDue = item.DaysUntilDue;
                string contactName = contact?.Name ?? "طرف غير محدد";
                string recipientName = $"{owner.FirstName} {owner.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(recipientName))
                    recipientName = owner.Email;

                // 1. Push notification to owner
                if (!log.PushSent)
                {
                    try
                    {
                        var pushTitle = item.Notification?.Title;
                        var pushBody = item.Notification?.Message;
                        if (pushTitle == null || pushBody == null)
                        {
                            var dummy = Notification.CreateInstallmentReminder(
                                owner.Id, transaction.Id, installment.Id, contactName, installment.Amount, installment.DueDate, daysUntilDue);
                            pushTitle = dummy.Title;
                            pushBody = dummy.Message;
                        }

                        var pushData = new Dictionary<string, string>
                        {
                            ["type"] = "InstallmentReminder",
                            ["notificationId"] = item.Notification?.Id.ToString() ?? string.Empty,
                            ["transactionId"] = transaction.Id.ToString(),
                            ["installmentId"] = installment.Id.ToString(),
                            ["screen"] = "notifications"
                        };

                        var pushTag = $"{installment.Id}-{stage}";

                        var pushResult = await _pushService.SendToUserAsync(
                            transaction.OwnerUserId,
                            new PushMessage(pushTitle, pushBody, pushData, pushTag),
                            ct);

                        bool isSuccess = pushResult.Sent > 0 || pushResult.TokensFound == 0;
                        if (isSuccess)
                        {
                            log.MarkPushSent();
                        }

                        _logger.LogInformation(
                            "FCM Push for InstallmentId={InstallmentId}, DueDate={DueDate:yyyy-MM-dd}, Stage={Stage}: {Status} (TokensFound={TokensFound}, Sent={Sent}, Failed={Failed})",
                            installment.Id, installment.DueDate, stage, isSuccess ? "نجح" : "فشل", pushResult.TokensFound, pushResult.Sent, pushResult.Failed);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "FCM Push for InstallmentId={InstallmentId}, DueDate={DueDate:yyyy-MM-dd}, Stage={Stage}: فشل (Exception: {Message})",
                            installment.Id, installment.DueDate, stage, ex.Message);
                    }
                }

                // 2. Email to owner
                if (!log.OwnerEmailSent)
                {
                    try
                    {
                        var emailSent = await _emailService.SendInstallmentReminderEmailAsync(
                            owner.Email,
                            recipientName,
                            contactName,
                            installment.Amount,
                            installment.DueDate,
                            daysUntilDue,
                            ct);

                        if (emailSent)
                        {
                            log.MarkOwnerEmailSent();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending owner email for installment {InstallmentId} to {Email}", installment.Id, owner.Email);
                    }
                }

                // 3. Email to contact (if applicable)
                if (contactEmailApplicable && !log.ContactEmailSent)
                {
                    try
                    {
                        var contactEmail = contact!.ContactEmail!;
                        var contactSent = await _emailService.SendInstallmentReminderToContactEmailAsync(
                            contactEmail,
                            contactName,
                            recipientName,
                            installment.Amount,
                            installment.DueDate,
                            daysUntilDue,
                            ct);

                        if (contactSent)
                        {
                            log.MarkContactEmailSent();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending contact email for installment {InstallmentId} to {Email}", installment.Id, contact?.ContactEmail);
                    }
                }

                log.RecordAttempt();

                if (item.IsNew)
                {
                    sentCount++;
                }
            }

            // Commit Phase 2 channel flags & attempt counts
            try
            {
                await _unitOfWork.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving channel flags during Phase 2 for stage {Stage}", stage);
            }
        }

        return sentCount;
    }
}
