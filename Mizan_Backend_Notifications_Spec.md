# Mizan Backend — Installment Reminders + Firebase Push (Implementation Spec)

> **Audience:** an AI coding agent working inside the `mizan_backend` repo (.NET 8, EF Core 8, SQL Server).
> **Scope:** backend only. The Flutter app is a separate, later phase (contract in §11).
> **Rule of thumb:** follow the existing architecture (Core → Application → Infrastructure → API, Repository + UnitOfWork, `Success(...)` response wrapper, Arabic user-facing messages). Do not refactor unrelated code.

---

## 0. Why this work exists (problem statement)

A user created a Sale with 3 automatic installments; the first installment was due **today**. `GET /api/notifications` returned an empty list. After manually calling `POST /api/notifications/run-reminders-scan`, the notification appeared (`createdAt` = the time of the manual call).

Root causes found in the code:

| # | Finding | Where |
|---|---------|-------|     
| 1 | In-app notifications are only created by `ReminderScanner`. It runs from a background loop (`Reminders:CheckIntervalMinutes`, **60** in `appsettings.json`) or the manual endpoint. **Nothing triggers it when a transaction is created.** | `ReminderCheckService`, `TransactionService.CreateAsync` |
| 2 | The scanner sends the owner email **first** and does `continue` if it fails → **no Notification and no log is created** if Brevo fails. In-app notification is wrongly coupled to email success. A test (`ScanAndProcessRemindersAsync_WhenEmailFails_LogIsNotSavedSoItRetriesLater`) codifies this. | `ReminderScanner.cs` |
| 3 | Scanner only matches `DueDate.Date == today+N` (N ∈ {3,1,0}). Overdue installments are never reminded. | `InstallmentRepository.GetPendingByDueDateAsync` |
| 4 | All date logic uses `DateTime.UtcNow.Date`. Users are in Egypt (UTC+2/+3). An installment due `2026-10-02T00:00:00+03:00` is stored as `2026-10-01T21:00Z` → shown **Overdue** on its due day and missed by the "due today" stage. | `ReminderScanner`, `TransactionService.MapToResponse` (≈ line 544) |
| 5 | Contact email failure is logged but never retried; log row is saved with `ContactEmailSent=false` forever. | `ReminderScanner.cs` |
| 6 | `POST /api/notifications/run-reminders-scan` runs a **global** scan (all users, sends emails to everyone) for any authenticated caller. | `NotificationsController` |
| 7 | No push support at all (no Firebase package, no device-token table/endpoint). | whole repo |

## 1. Target behaviour (the whole system)

Parties per installment reminder:

| Party | Email | In-app Notification | Push (FCM) |
|-------|-------|---------------------|------------|
| Shop owner (app user) | ✅ | ✅ | ✅ |
| Contact (customer/supplier, not an app user) | ✅ only if `Contact.ContactEmail` is set | ❌ | ❌ |

Reminder stages per installment (each sent **once**): `Reminders:DaysBeforeDue` (default `[3,1]`) + stage `0` (due today). Stage `0` also covers **overdue** installments up to `OverdueLookbackDays` back.

Flow:

```
Transaction created ─► (if an installment is due within the stages) enqueue ownerUserId in ReminderTriggerChannel
                         └► ReminderTriggerWorker ─► IReminderScanner.ScanForOwnerAsync(ownerUserId)
Background loop (every CheckIntervalMinutes) ─► IReminderScanner.ScanAndProcessRemindersAsync()  (all users)

Scanner, per (installment, stage) that has no log yet:
   1. create Notification + InstallmentReminderLog  → SaveChanges        (NEVER depends on email/push)
   2. send FCM push to the owner's devices                               (best effort)
   3. send email to owner                                                (best effort)
   4. send email to contact if it has an email                           (best effort, independent of #3)
   5. record per-channel success flags + attempt count → SaveChanges
Per (installment, stage) with a log that is incomplete: retry only the failed channels (max 3 attempts, 5-minute backoff).
```

Hard guarantees:
- A failing/invalid **contact email** never affects the owner's Notification, push or email.
- A failing **owner email** (Brevo down) never prevents the owner's Notification or push.
- No duplicate Notification for the same (installment, stage), even with concurrent scans.

---

## 2. Work breakdown (do in this order, build + test after each step)

### Step 1 — Egypt time helper (Core)

Create `src/Mizan.Core/Time/EgyptTime.cs` (static):

```csharp
public static class EgyptTime
{
    // Resolve "Africa/Cairo", fall back to "Egypt Standard Time" (Windows hosting). Cache in a static readonly field.
    public static TimeZoneInfo Zone { get; }

    public static DateTime TodayLocal(DateTime? utcNow = null);          // local calendar date (Kind=Unspecified, 00:00)
    public static DateTime ToLocalDate(DateTime storedDueDate);          // treat Kind Unspecified as UTC, convert to Cairo, return .Date
    public static (DateTime StartUtc, DateTime EndUtcExclusive) LocalDateRangeToUtc(DateTime fromLocalDate, DateTime toLocalDateInclusive);
}
```

Implementation notes:
- Values coming back from SQL Server have `Kind=Unspecified`; treat them as UTC (`DateTime.SpecifyKind(x, DateTimeKind.Utc)`).
- Egypt DST transitions happen **at local midnight** (spring-forward makes `00:00` an *invalid* local time). When converting a local midnight to UTC: `if (Zone.IsInvalidTime(dt)) dt = dt.AddHours(1);` before `TimeZoneInfo.ConvertTimeToUtc`. Never let this throw.
- `LocalDateRangeToUtc(a, b)` returns `[midnight(a) in UTC, midnight(b+1 day) in UTC)`.

Unit-test it: `2026-10-01T21:00:00Z → ToLocalDate == 2026-10-02` (Cairo is UTC+3 on that date), `2026-10-02T00:00:00Z → 2026-10-02`, and a DST-transition date.

### Step 2 — Database: device tokens + reminder-log retry columns

**New entity** `src/Mizan.Core/Entities/DeviceToken.cs`:
`Id (Guid)`, `UserId (Guid, FK users, cascade)`, `Token (string, required, max 512)`, `Platform (DevicePlatform enum: Android=0, iOS=1, Web=2)`, `DeviceId (string?, max 128)`, `CreatedAt`, `UpdatedAt`, `LastSeenAt`. Private ctor for EF + static `Create(...)` + `Touch()` + `Reassign(userId)`.
Table `device_tokens`, snake_case columns like the other configurations. **Unique index on `token`.** Index on `user_id`.

**Extend `InstallmentReminderLog`** (keep `Create(installmentId, daysBeforeDue, contactEmailSent=false)` working):
- `OwnerEmailSent` (bool, default false) → column `owner_email_sent`
- `PushSent` (bool, default false) → `push_sent`
- `Attempts` (int, default 0) → `attempts`
- `LastAttemptAt` (DateTime?) → `last_attempt_at`
- methods: `MarkOwnerEmailSent()`, `MarkContactEmailSent()`, `MarkPushSent()`, `RecordAttempt()`, `IsComplete(bool contactEmailApplicable)`, `ShouldRetry(bool contactEmailApplicable, int maxAttempts, TimeSpan backoff, DateTime utcNow)`.
- Keep the existing unique index `(installment_id, days_before_due)`.

**Repositories** (Core interfaces + Infrastructure impls + `IUnitOfWork`/`UnitOfWork` property `DeviceTokens` + DI in `Program.cs` + `MizanDbContext` DbSet + `DeviceTokenConfiguration`):
- `IDeviceTokenRepository`: `GetByTokenAsync(token)`, `GetByUserIdAsync(userId)`, `CountByUserIdAsync(userId)`, `AddAsync`, `Remove`, `RemoveRange`.
- `IInstallmentReminderLogRepository`: add `GetByInstallmentIdsAsync(IReadOnlyList<Guid> ids, int daysBeforeDue)` → `Dictionary<Guid, InstallmentReminderLog>` (**tracked** entities, so updates persist). Keep the old methods if still used by tests; otherwise remove and update tests.
- `IInstallmentRepository`: **replace** `GetPendingByDueDateAsync` with
  `GetPendingByDueRangeAsync(DateTime startUtc, DateTime endUtcExclusive, Guid? ownerUserId, CancellationToken)`:
  `Status == Pending && Transaction.IsActive && Transaction.Owner.IsActive && DueDate >= startUtc && DueDate < endUtcExclusive && (ownerUserId == null || Transaction.OwnerUserId == ownerUserId)`, with `Include(Transaction → Owner, Contact)`. **Do not use `.Date` in the LINQ predicate** (range comparison is index-friendly and timezone-correct).

**Migration** (name e.g. `AddDeviceTokensAndReminderRetry`):
```
dotnet ef migrations add AddDeviceTokensAndReminderRetry --project src/Mizan.Infrastructure --startup-project src/Mizan.API
```
Edit the generated migration's `Up` to **backfill existing log rows** so old reminders are not re-sent/retried after deploy:
```csharp
migrationBuilder.Sql(@"UPDATE installment_reminder_logs
                       SET owner_email_sent = 1, push_sent = 1, attempts = 3");
```
(Add the new columns first, then run the UPDATE.) Provide a correct `Down`.

### Step 3 — Options / config

`RemindersOptions` (`src/Mizan.Application/DTOs/Notifications/RemindersOptions.cs`):
```csharp
public int CheckIntervalMinutes { get; set; } = 5;     // was 60
public int OverdueLookbackDays  { get; set; } = 7;     // stage 0 also covers overdue within N days
public int MaxChannelAttempts   { get; set; } = 3;
public int RetryBackoffMinutes  { get; set; } = 5;
```
New `FirebaseOptions` (Infrastructure, section `"Firebase"`):
```csharp
public bool Enabled { get; set; } = false;
public string? ServiceAccountJson { get; set; }   // raw JSON, from env var Firebase__ServiceAccountJson
public string? ServiceAccountPath { get; set; }   // alternative: path to the json file
public string AndroidChannelId { get; set; } = "mizan_reminders";
```
Update `appsettings.json` and `appsettings.Development.json`: `Reminders.CheckIntervalMinutes = 5` (Development may stay 1), add `OverdueLookbackDays`, `MaxChannelAttempts`, `RetryBackoffMinutes`, and a `Firebase` section with `"Enabled": false` and **no secrets**. Add `firebase-service-account*.json` to `.gitignore`.

### Step 4 — Push notification service

**Application** (`src/Mizan.Application/Interfaces/IPushNotificationService.cs`):
```csharp
public record PushMessage(string Title, string Body, IReadOnlyDictionary<string,string> Data);
public record PushSendResult(int TokensFound, int Sent, int Failed);
public interface IPushNotificationService
{
    Task<PushSendResult> SendToUserAsync(Guid userId, PushMessage message, CancellationToken ct = default);
}
```

**Infrastructure**:
- Add NuGet `FirebaseAdmin` (latest stable 3.x, net8.0-compatible) to `Mizan.Infrastructure.csproj`.
- `FcmPushNotificationService`:
  - Initialise `FirebaseApp` once (guard `FirebaseApp.DefaultInstance == null`), credential from `ServiceAccountJson` (preferred) or `ServiceAccountPath`.
  - Load the user's tokens through `IUnitOfWork.DeviceTokens`. If none → return `(0,0,0)`.
  - Build one `Message` per token (`FirebaseMessaging.SendEachAsync`): `Notification {Title, Body}`, `Data` (all values **strings**), `Android { Priority = High, Notification { ChannelId = options.AndroidChannelId, Sound = "default" } }`, `Apns { Aps { Sound = "default" } }`.
  - For failed sends: if `MessagingErrorCode` is `Unregistered` or `SenderIdMismatch` → delete that token and save. For other errors only log a warning.
  - Never throw to the caller; catch everything, log, return counts. **Never log a full token** (last 6 chars at most).
- `NoOpPushNotificationService` (returns `(0,0,0)`, logs at Debug). Register it when `Firebase:Enabled=false`, when running in the `Testing` environment, or when credentials are missing/invalid — **the app must still boot** (unlike Brevo's fail-fast, do not throw at startup).

### Step 5 — Device-token endpoints

`IDeviceTokenService` / `DeviceTokenService` (Application) + `DevicesController` (`[Authorize]`, `[EnableRateLimiting("GeneralPolicy")]`, route `api/devices`):

| Method | Route | Body | Behaviour |
|--------|-------|------|-----------|
| POST | `/api/devices/register` | `{ "token": "...", "platform": "Android"\|"iOS"\|"Web", "deviceId": "optional" }` | **Upsert by token.** If the token exists for another user → reassign to the current user (same phone, different account). If it exists for this user → `Touch()`. Max **10** tokens per user: delete the oldest by `LastSeenAt` when exceeded. Returns `Success(null, "تم تسجيل الجهاز بنجاح")`. |
| POST | `/api/devices/unregister` | `{ "token": "..." }` | Deletes the token **only if it belongs to the current user**; idempotent (200 even if not found). |

Validation: token required, 1–512 chars (400 otherwise, Arabic message); platform must parse (the app already uses `JsonStringEnumConverter`).

Also extend `LogoutRequest` with optional `DeviceToken`; in `AuthController.Logout`, after revoking the refresh token, call `IDeviceTokenService.UnregisterAsync(CurrentUserId, request.DeviceToken)` when provided. Do not change the existing success response.

### Step 6 — Rewrite `ReminderScanner` (core of the task)

`IReminderScanner` keeps `ScanAndProcessRemindersAsync(DateTime? referenceDate, CancellationToken)` (all users; `referenceDate` is a UTC instant, default `UtcNow`) and gains:
```csharp
Task<int> ScanForOwnerAsync(Guid ownerUserId, DateTime? referenceDate = null, CancellationToken ct = default);
```
Both call one private `ScanCoreAsync(Guid? ownerUserId, ...)`.

Constructor adds `IPushNotificationService`. Serialize scans inside the process with a **static `SemaphoreSlim(1,1)`** (WaitAsync with the cancellation token) so the background loop, the trigger worker and the manual endpoint never run concurrently.

Algorithm (per scan):
```
if !options.Enabled → return 0
today  = EgyptTime.TodayLocal(referenceDate)
stages = (DaysBeforeDue ∪ {0}).Distinct().OrderByDescending()
foreach stage:
    if stage == 0: range = LocalDateRangeToUtc(today - OverdueLookbackDays, today)
    else           range = LocalDateRangeToUtc(today + stage, today + stage)
    installments = repo.GetPendingByDueRangeAsync(range, ownerUserId)
    logs         = logRepo.GetByInstallmentIdsAsync(installments ids, stage)      // tracked
    newItems = []  // (installment, notification, log)
    foreach installment:
        daysUntilDue = (EgyptTime.ToLocalDate(installment.DueDate) - today).Days   // <= 0 for stage 0, negative = overdue
        log = logs.get(installment.Id)
        if log == null:
            notification = Notification.CreateInstallmentReminder(..., daysUntilDue)
            log = InstallmentReminderLog.Create(installment.Id, stage)
            add both; mark as "new"
        else if !log.ShouldRetry(...): continue
        work.Add((installment, log, notification?))
    SaveChanges  // PHASE 1: persists Notifications + logs BEFORE any network call.
                 // Catch DbUpdateException (unique-index race) → log warning, continue to next stage.
    foreach work item (sequential; or Parallel with max 4):
        contactEmailApplicable = !string.IsNullOrWhiteSpace(contact?.ContactEmail)
        if !log.PushSent:         result = push.SendToUserAsync(ownerId, new PushMessage(title, message, data))
                                  if result.Sent > 0 || result.TokensFound == 0 → log.MarkPushSent()   // no tokens = nothing to retry
        if !log.OwnerEmailSent:   if await email.SendInstallmentReminderEmailAsync(...) → log.MarkOwnerEmailSent()
        if contactEmailApplicable && !log.ContactEmailSent:
                                  if await email.SendInstallmentReminderToContactEmailAsync(...) → log.MarkContactEmailSent()
        log.RecordAttempt()
        sentCount++ only for newly created reminders
    SaveChanges  // PHASE 2: persists channel flags
```
- For a retry pass (existing log) the Notification already exists; reload it only if needed for the push text — simplest: rebuild title/body with the same `Notification.CreateInstallmentReminder` text logic (or store nothing new) so no extra query is required. Do **not** create a second Notification on retry.
- Push `data` payload (strings): `type = "InstallmentReminder"`, `notificationId`, `transactionId`, `installmentId`, `screen = "notifications"`.
- Each channel call is wrapped in try/catch; an exception in one channel/installment must not abort the loop.
- Return value = number of **new** reminders created (keeps `run-reminders-scan` response meaning).

Also update:
- `Notification.CreateInstallmentReminder`: when `daysUntilDue < 0` → title `"قسط متأخر"`, message `$"قسط بقيمة {amount} متأخر منذ {n} يوم للطرف {contactName} (تاريخ الاستحقاق: {dueDate:yyyy-MM-dd})"` (use correct Arabic dual/plural like the existing code: يوم واحد / يومين / N أيام).
- `EmailService` (owner + contact reminder methods): the `dueText` switch must handle negative `daysUntilDue` ("متأخر منذ N أيام") and the subject for overdue. Keep the existing templates/branding.

### Step 7 — Trigger on transaction creation (no blocking, no global scan)

- `IReminderTriggerChannel` (Application) with `bool TryQueue(Guid ownerUserId)`; `ReminderTriggerChannel` (Infrastructure, **singleton**, `Channel<Guid>` bounded 500, `FullMode = DropOldest`, mirrors `PeriodicReportEmailChannel`).
- `ReminderTriggerWorker : BackgroundService` — reads the channel, creates a scope, calls `IReminderScanner.ScanForOwnerAsync(ownerUserId)`; catches/logs all exceptions; honours cancellation. Register both in `Program.cs`.
- `TransactionService.CreateAsync`: **after** the successful `SaveChangesAsync`, if the transaction has installments and `min(EgyptTime.ToLocalDate(i.DueDate)) <= EgyptTime.TodayLocal() + max(stages)`, call `_reminderTrigger.TryQueue(ownerUserId)` inside try/catch (same defensive style as the periodic-report trigger already there). Inject `IReminderTriggerChannel` into the constructor and update **every** place that constructs `TransactionService` (tests).
- `TransactionService.MapToResponse` (≈ line 544): overdue check becomes `EgyptTime.ToLocalDate(i.DueDate) < EgyptTime.TodayLocal()`.
- `NotificationsController.RunRemindersScan`: call `ScanForOwnerAsync(CurrentUserId)` instead of the global scan; keep the same response shape.
- `ReminderCheckService`: no logic change needed (it already loops with `CheckIntervalMinutes`); confirm it reads the new default.

### Step 8 — Tests (update + add; the suite must be green)

Update existing tests broken by the signature/semantic changes (`ReminderScannerTests`, `ReminderScannerContactEmailTests`, `TransactionServiceTests`, `NotificationServiceTests`, integration `CustomWebApplicationFactory` — register `NoOpPushNotificationService` and a no-op trigger channel for the `Testing` environment). Rename/rewrite `WhenEmailFails_LogIsNotSavedSoItRetriesLater` → **`WhenOwnerEmailFails_NotificationAndLogAreStillCreated_AndEmailIsRetried`**.

Add tests:
1. `EgyptTime`: UTC→Cairo date conversion, boundary `2026-10-01T21:00Z`, DST-midnight invalid-time handling.
2. Scanner: stage 0 matches an installment stored as `…T21:00Z` on the previous UTC day.
3. Scanner: overdue installment within `OverdueLookbackDays` gets exactly one overdue notification; older one gets none.
4. Scanner: no duplicate Notification on repeated scans / concurrent calls.
5. Scanner: owner email fails → Notification + push still happen; log has `OwnerEmailSent=false`; second scan (after backoff) retries and sets the flag; stops after `MaxChannelAttempts`.
6. Scanner: contact email invalid/failing → owner flow unaffected.
7. Scanner: contact without email → no contact send attempted, log can reach `IsComplete`.
8. Scanner: push service called with the expected payload; user with no tokens → `PushSent` becomes true without retries.
9. `ScanForOwnerAsync` only touches that owner's installments.
10. `DeviceTokenService`: register new, re-register same token (touch), token moved to another user, 10-token cap, unregister not owned (no-op), unregister idempotent.
11. `FcmPushNotificationService` token cleanup on `Unregistered` (abstract the FCM call behind a small internal interface so this is testable without network).
12. Integration: `POST /api/devices/register` + `/unregister` (auth required, validation 400s); creating an installment transaction due today results in a Notification appearing via `GET /api/notifications` within a few seconds **without** calling `run-reminders-scan` (poll with timeout).

### Step 9 — Docs & Postman

- Update `Mizan_API.postman_collection.json`: add a **Devices** folder (register / unregister) and adjust the description of 7.4 (now scans only the caller's installments). Mention optional `deviceToken` in the logout request.
- Update `API_DOCUMENTATION.md` and `README.md` (new endpoints, new config keys, Firebase setup, the reminder lifecycle in §1).

---

## 3. Constraints (do / don't)

- Do keep all existing endpoints and response shapes working except the intentional changes listed above.
- Do keep Arabic messages for end users; code, comments and logs in English.
- Do use `CancellationToken` everywhere and the existing UnitOfWork pattern.
- Do not commit secrets or the Firebase service-account file.
- Do not log full FCM tokens, emails of third parties at Info level beyond what the code already does.
- Do not make the app fail to start if Firebase is not configured.
- Do not call the global scan from request paths.
- Do not remove tests; adapt them.
- Run `dotnet build` and `dotnet test` at the end of each step; fix before moving on.

## 4. Acceptance checklist (manual, against a dev DB)

1. Create a Sale with 3 monthly installments, first due **today (Cairo date)** → within ~5 seconds `GET /api/notifications` shows the reminder **without** calling `run-reminders-scan`.
2. With an invalid Brevo key: the Notification still exists; `installment_reminder_logs.owner_email_sent = 0`; later scans retry ≤ 3 times then stop.
3. Contact with a malformed/non-existent email: owner still receives Notification + push + email.
4. With `Firebase:Enabled=true` and a registered token: a push arrives with title/body equal to the Notification and `data.screen = "notifications"`. After the app is uninstalled, the next send removes the dead token.
5. Installment stored as `2026-10-01T21:00:00Z`: shown as **not overdue** on `2026-10-02` (Cairo) and reminded as "due today".
6. Installment overdue by 3 days with no prior log → exactly one "قسط متأخر" notification; overdue by 30 days → none.
7. Running the scan 3× in a row creates no duplicates.
8. `dotnet build` and `dotnet test` pass; the migration applies cleanly on an existing DB and existing log rows are backfilled (no old reminders re-sent).

## 5. Ops notes (not code, for the human)

- The background scan only runs while the process is alive. On hosts that idle-sleep apps (e.g. runasp.net free tiers) enable "Always On" or ping `GET /health` every ≤ 5 minutes from an external uptime monitor; otherwise reminders/pushes will be late.
- Firebase: create project → add Android/iOS apps → download a **Service Account** key (Project settings → Service accounts) → set env var `Firebase__ServiceAccountJson` (raw JSON) and `Firebase__Enabled=true` on the server. iOS additionally needs an APNs key uploaded in Firebase Cloud Messaging settings.
- Brevo: the sender (`Email:SenderEmail`) must be a verified sender in Brevo; otherwise emails fail (now harmlessly for in-app/push).

## 6. Optional (only if time allows; separate commit)

- `StatisticsController` default `date ?? DateTime.UtcNow.Date` → `EgyptTime.TodayLocal()` (daily stats for Egyptian users between 00:00–03:00).
- Send a push when a `PeriodicReportReady` notification is created (same `IPushNotificationService`, `type = "PeriodicReportReady"`, `periodicReportId` in data).

## 11. Contract for the Flutter phase (do not implement now; keep payloads stable)

- Register token: `POST /api/devices/register` `{ "token", "platform": "Android|iOS", "deviceId" }` after login and on every FCM token refresh. Unregister on logout (`/api/devices/unregister` or `deviceToken` in the logout body).
- Android notification channel id must be **`mizan_reminders`** (matches `Firebase:AndroidChannelId`).
- Push payload: `notification {title, body}` + `data { type, notificationId, transactionId, installmentId, screen }`; tapping opens the Notifications screen then `GET /api/notifications`.
- The app should **not** call `run-reminders-scan` for normal operation anymore (server does it).
