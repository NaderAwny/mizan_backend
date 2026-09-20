using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Mizan.Application.DTOs.Auth;
using Mizan.Application.Interfaces;
using Mizan.Application.Services;
using Mizan.Core.Entities;
using Mizan.Core.Exceptions;
using Mizan.Core.Interfaces;
using Mizan.Infrastructure.Persistence;
using Mizan.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Mizan.UnitTests.Services;

public class AuthServiceTests
{
    private class FakeEmailService : IEmailService
    {
        public List<(string Email, string Code)> SentOtps { get; } = new();

        public Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default)
        {
            SentOtps.Add((toEmail, otpCode));
            return Task.FromResult(true);
        }

        public Task<bool> SendInstallmentReminderEmailAsync(string toEmail, string recipientName, string contactName, decimal amount, DateTime dueDate, int daysUntilDue, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> SendInstallmentReminderToContactEmailAsync(string toEmail, string contactName, string shopOwnerName, decimal amount, DateTime dueDate, int daysUntilDue, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> SendPeriodicReportEmailAsync(string toEmail, string recipientName, int batchNumber, byte[] pdfBytes, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private class FakeEmailVerificationService : IEmailVerificationService
    {
        public bool IsDeliverable { get; set; } = true;

        public Task<EmailVerificationResult> VerifyAsync(string email, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsDeliverable
                ? EmailVerificationResult.Valid()
                : EmailVerificationResult.Invalid("Domain invalid"));
        }
    }

    private class FakeJwtProvider : IJwtProvider
    {
        public int AccessTokenExpirationSeconds => 1800;
        public int RefreshTokenExpirationDays => 30;
        public string GenerateAccessToken(User user) => "fake_jwt_token_" + user.Id;
        public string GenerateRefreshToken() => "fake_refresh_token_" + Guid.NewGuid();
    }

    private class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Mizan";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static MizanDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MizanDbContext(options);
    }

    private static (AuthService Service, MizanDbContext Db, FakeEmailService EmailService) CreateService(MizanDbContext? db = null, IUnitOfWork? customUow = null)
    {
        var context = db ?? CreateDb();
        var uow = customUow ?? new UnitOfWork(context);
        var emailService = new FakeEmailService();
        var emailVerificationService = new FakeEmailVerificationService();
        var jwtProvider = new FakeJwtProvider();
        var env = new FakeHostEnvironment();
        var logger = NullLogger<AuthService>.Instance;

        var service = new AuthService(uow, jwtProvider, emailService, emailVerificationService, env, logger);
        return (service, context, emailService);
    }

    // ── 1. تسجيل إيميل جديد → يوزر غير مفعّل + OTP يتبعت ──────────────────────

    [Fact]
    public async Task RegisterAsync_NewEmail_CreatesInactiveUserAndSendsOtp()
    {
        var (service, db, emailService) = CreateService();
        var request = new RegisterRequest
        {
            Email = "newuser@mizan.app",
            FirstName = "نادر",
            LastName = "عوني"
        };

        var response = await service.RegisterAsync(request);

        Assert.True(response.OtpSent);
        Assert.Single(emailService.SentOtps);
        Assert.Equal("newuser@mizan.app", emailService.SentOtps[0].Email);

        var createdUser = await db.Set<User>().FirstOrDefaultAsync(u => u.Email == "newuser@mizan.app");
        Assert.NotNull(createdUser);
        Assert.False(createdUser.IsActive);
        Assert.Equal("نادر", createdUser.FirstName);
        Assert.Equal("عوني", createdUser.LastName);
    }

    // ── 2. تسجيل إيميل موجود وغير مفعّل بأسامي مختلفة → تحديث الاسم + إعادة إرسال OTP ───

    [Fact]
    public async Task RegisterAsync_ExistingInactiveUser_UpdatesNameAndResendsOtp()
    {
        var (service, db, emailService) = CreateService();

        // Arrange: unverified inactive user exists
        var user = User.Create("pending@mizan.app", "الاسم", "القديم");
        user.Deactivate();
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();

        var request = new RegisterRequest
        {
            Email = "pending@mizan.app",
            FirstName = "الاسم",
            LastName = "الجديد"
        };

        // Act
        var response = await service.RegisterAsync(request);

        // Assert
        Assert.True(response.OtpSent);
        Assert.Single(emailService.SentOtps);

        var updatedUser = await db.Set<User>().FirstOrDefaultAsync(u => u.Email == "pending@mizan.app");
        Assert.NotNull(updatedUser);
        Assert.False(updatedUser.IsActive);
        Assert.Equal("الاسم", updatedUser.FirstName);
        Assert.Equal("الجديد", updatedUser.LastName);
    }

    // ── 3. تسجيل إيميل موجود ومفعّل → BadRequestException برسالة "مسجل بالفعل" ─────

    [Fact]
    public async Task RegisterAsync_ExistingActiveUser_ThrowsBadRequestException()
    {
        var (service, db, emailService) = CreateService();

        // Arrange: active user already registered
        var activeUser = User.Create("active@mizan.app", "محمد", "علي");
        Assert.True(activeUser.IsActive);
        db.Set<User>().Add(activeUser);
        await db.SaveChangesAsync();

        var request = new RegisterRequest
        {
            Email = "active@mizan.app",
            FirstName = "محمود",
            LastName = "حسن"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.RegisterAsync(request));

        Assert.Contains("مسجل بالفعل بحساب آخر", ex.Message);
        Assert.Empty(emailService.SentOtps); // No OTP sent!

        // Profile should not have changed
        var unchangedUser = await db.Set<User>().FirstOrDefaultAsync(u => u.Email == "active@mizan.app");
        Assert.NotNull(unchangedUser);
        Assert.Equal("محمد", unchangedUser.FirstName);
        Assert.Equal("علي", unchangedUser.LastName);
    }

    // ── 4. Simulate الـ race condition في RegisterAsync ──────────────────────

    private class ConcurrencyFaultingUnitOfWork : IUnitOfWork
    {
        private readonly IUnitOfWork _inner;
        private readonly bool _failOnSave;

        public ConcurrencyFaultingUnitOfWork(IUnitOfWork inner, bool failOnSave = true)
        {
            _inner = inner;
            _failOnSave = failOnSave;
        }

        public IUserRepository Users => _inner.Users;
        public IShopRepository Shops => _inner.Shops;
        public IRefreshTokenRepository RefreshTokens => _inner.RefreshTokens;
        public IOtpCodeRepository OtpCodes => _inner.OtpCodes;
        public IContactRepository Contacts => _inner.Contacts;
        public ITransactionRepository Transactions => _inner.Transactions;
        public IInstallmentRepository Installments => _inner.Installments;
        public INotificationRepository Notifications => _inner.Notifications;
        public IInstallmentReminderLogRepository InstallmentReminderLogs => _inner.InstallmentReminderLogs;
        public IPeriodicReportRepository PeriodicReports => _inner.PeriodicReports;
        public IVoiceNoteRepository VoiceNotes => _inner.VoiceNotes;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_failOnSave)
            {
                throw new DbUpdateException(
                    "Cannot insert duplicate key row in object 'users' with unique index 'IX_users_email'. (Error 2601)",
                    new Exception("Violation of UNIQUE KEY constraint 'IX_users_email'. Cannot insert duplicate key (2601)."));
            }
            return _inner.SaveChangesAsync(cancellationToken);
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => _inner.BeginTransactionAsync(cancellationToken);
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => _inner.CommitTransactionAsync(cancellationToken);
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => _inner.RollbackTransactionAsync(cancellationToken);
        public void Dispose() => _inner.Dispose();
    }

    [Fact]
    public async Task RegisterAsync_RaceCondition_DbUpdateException_ThrowsCleanBadRequestException()
    {
        var db = CreateDb();
        var realUow = new UnitOfWork(db);
        var faultingUow = new ConcurrencyFaultingUnitOfWork(realUow, failOnSave: true);

        var (service, _, _) = CreateService(db, faultingUow);

        var request = new RegisterRequest
        {
            Email = "raced@mizan.app",
            FirstName = "سباق",
            LastName = "تزامن"
        };

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.RegisterAsync(request));
        Assert.Contains("مسجل بالفعل بحساب آخر", ex.Message);
    }

    // ── 5. Simulate الـ race condition في VerifyOtpAsync ───────────────────────

    private class VerifyOtpRaceUnitOfWork : IUnitOfWork
    {
        private readonly IUnitOfWork _inner;
        private readonly MizanDbContext _db;
        private readonly string _targetEmail;
        private bool _hasFailedSave = false;

        public VerifyOtpRaceUnitOfWork(IUnitOfWork inner, MizanDbContext db, string targetEmail)
        {
            _inner = inner;
            _db = db;
            _targetEmail = targetEmail;
        }

        public IUserRepository Users => _inner.Users;
        public IShopRepository Shops => _inner.Shops;
        public IRefreshTokenRepository RefreshTokens => _inner.RefreshTokens;
        public IOtpCodeRepository OtpCodes => _inner.OtpCodes;
        public IContactRepository Contacts => _inner.Contacts;
        public ITransactionRepository Transactions => _inner.Transactions;
        public IInstallmentRepository Installments => _inner.Installments;
        public INotificationRepository Notifications => _inner.Notifications;
        public IInstallmentReminderLogRepository InstallmentReminderLogs => _inner.InstallmentReminderLogs;
        public IPeriodicReportRepository PeriodicReports => _inner.PeriodicReports;
        public IVoiceNoteRepository VoiceNotes => _inner.VoiceNotes;

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_hasFailedSave)
            {
                _hasFailedSave = true;
                // Simulate that another concurrent request committed the user right now
                var winningUser = User.Create(_targetEmail, "مستخدم", "سابق");
                _db.Set<User>().Add(winningUser);
                await _db.SaveChangesAsync(cancellationToken);

                throw new DbUpdateException(
                    "Cannot insert duplicate key row in object 'users' with unique index 'IX_users_email'.",
                    new Exception("Violation of UNIQUE KEY constraint 'IX_users_email' (2601)."));
            }

            return await _inner.SaveChangesAsync(cancellationToken);
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => _inner.BeginTransactionAsync(cancellationToken);
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => _inner.CommitTransactionAsync(cancellationToken);
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => _inner.RollbackTransactionAsync(cancellationToken);
        public void Dispose() => _inner.Dispose();
    }

    [Fact]
    public async Task VerifyOtpAsync_RaceCondition_RecoversAndReturnsAuthResponse()
    {
        var db = CreateDb();
        var email = "verify_race@mizan.app";
        var code = "654321";

        // Seed valid OTP
        var otp = OtpCode.Create(email, code, 300);
        db.Set<OtpCode>().Add(otp);
        await db.SaveChangesAsync();

        var realUow = new UnitOfWork(db);
        var raceUow = new VerifyOtpRaceUnitOfWork(realUow, db, email);
        var (service, _, _) = CreateService(db, raceUow);

        var request = new VerifyOtpRequest
        {
            Email = email,
            Code = code
        };

        var response = await service.VerifyOtpAsync(request);

        Assert.NotNull(response);
        Assert.NotEmpty(response.Token);
        Assert.Equal(email, response.Email);
    }

    // ── 6. تأكيد التطبيع (Normalization) للإيميل ─────────────────────────────

    [Fact]
    public async Task RegisterAsync_EmailNormalization_NormalizesEmailCorrectly()
    {
        var (service, db, _) = CreateService();

        var request = new RegisterRequest
        {
            Email = "  MiXeD.CaSe@ExAmPlE.CoM  ",
            FirstName = "نادر",
            LastName = "عوني"
        };

        await service.RegisterAsync(request);

        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Email == "mixed.case@example.com");
        Assert.NotNull(user);
        Assert.Equal("mixed.case@example.com", user.Email);

        // Attempting to register again with same email in lowercase should be rejected
        user.Activate();
        await db.SaveChangesAsync();

        var duplicateRequest = new RegisterRequest
        {
            Email = "mixed.case@example.com",
            FirstName = "شخص",
            LastName = "آخر"
        };

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.RegisterAsync(duplicateRequest));
        Assert.Contains("مسجل بالفعل بحساب آخر", ex.Message);
    }

    [Fact]
    public async Task SendOtpAsync_EmailNormalization_FindsUserWithNormalizedEmail()
    {
        var (service, db, emailService) = CreateService();

        var activeUser = User.Create("normalized@mizan.app", "أحمد", "علي");
        db.Set<User>().Add(activeUser);
        await db.SaveChangesAsync();

        var response = await service.SendOtpAsync("  NoRmAlIzEd@MiZaN.ApP  ");

        Assert.True(response.OtpSent);
        Assert.Single(emailService.SentOtps);
        Assert.Equal("normalized@mizan.app", emailService.SentOtps[0].Email);
    }

    [Fact]
    public async Task VerifyOtpAsync_EmailNormalization_VerifiesWithNormalizedEmail()
    {
        var (service, db, _) = CreateService();
        var email = "otp_normalized@mizan.app";
        var code = "112233";

        var otp = OtpCode.Create(email, code, 300);
        db.Set<OtpCode>().Add(otp);
        await db.SaveChangesAsync();

        var request = new VerifyOtpRequest
        {
            Email = "  OtP_NoRmAlIzEd@MiZaN.ApP  ",
            Code = code
        };

        var response = await service.VerifyOtpAsync(request);

        Assert.NotNull(response);
        Assert.Equal("otp_normalized@mizan.app", response.Email);
    }
}
