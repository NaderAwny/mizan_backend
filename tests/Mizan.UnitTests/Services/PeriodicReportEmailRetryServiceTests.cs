using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mizan.Application.DTOs.Reports;
using Mizan.Application.Interfaces;
using Mizan.Core.Entities;
using Mizan.Core.Interfaces;
using Mizan.Infrastructure.BackgroundServices;
using Mizan.Infrastructure.Persistence;
using Xunit;

namespace Mizan.UnitTests.Services;

public class PeriodicReportEmailRetryServiceTests
{
    private class FakeEmailService : IEmailService
    {
        public bool ShouldSucceed { get; set; } = true;
        public int SendCount { get; private set; }
        public string? LastRecipientEmail { get; private set; }

        public Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> SendInstallmentReminderEmailAsync(
            string toEmail, string recipientName, string contactName, decimal amount, DateTime dueDate, int daysUntilDue, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> SendPeriodicReportEmailAsync(
            string toEmail, string recipientName, int batchNumber, byte[] pdfBytes, CancellationToken cancellationToken = default)
        {
            SendCount++;
            LastRecipientEmail = toEmail;
            return Task.FromResult(ShouldSucceed);
        }
    }

    private static MizanDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new MizanDbContext(options);
    }

    private static IServiceScopeFactory CreateScopeFactory(string dbName, IEmailService emailService)
    {
        var services = new ServiceCollection();
        services.AddDbContext<MizanDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton(emailService);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IServiceScopeFactory>();
    }

    private class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T currentValue) => CurrentValue = currentValue;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    [Fact]
    public async Task RetryService_WhenUnsentReportExists_ShouldSendEmailAndMarkSent()
    {
        // Arrange
        string dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        var emailService = new FakeEmailService { ShouldSucceed = true };
        var scopeFactory = CreateScopeFactory(dbName, emailService);

        int ownerUserId = 1;
        string tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pdf");
        await File.WriteAllBytesAsync(tempFile, new byte[] { 1, 2, 3, 4 });

        try
        {
            var user = User.Create("owner@test.com", "أحمد", "علي", "shop_owner");
            var report = PeriodicReport.Create(ownerUserId, 1, 7, 500, 100, tempFile);

            db.Set<User>().Add(user);
            db.Set<PeriodicReport>().Add(report);
            await db.SaveChangesAsync();

            var optionsMonitor = new TestOptionsMonitor<PeriodicReportsOptions>(new PeriodicReportsOptions
            {
                Enabled = true,
                CheckIntervalMinutes = 1
            });

            var service = new PeriodicReportEmailRetryService(
                scopeFactory,
                optionsMonitor,
                NullLogger<PeriodicReportEmailRetryService>.Instance);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(400);

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(100);
            await service.StopAsync(CancellationToken.None);

            // Assert
            Assert.True(emailService.SendCount >= 1);
            Assert.Equal("owner@test.com", emailService.LastRecipientEmail);

            using var verifyDb = CreateDb(dbName);
            var updatedReport = await verifyDb.Set<PeriodicReport>().FindAsync(report.Id);
            Assert.NotNull(updatedReport);
            Assert.True(updatedReport.EmailSent);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RetryService_WhenAllReportsAlreadySent_ShouldNotCallEmailService()
    {
        // Arrange
        string dbName = Guid.NewGuid().ToString();
        using var db = CreateDb(dbName);
        var emailService = new FakeEmailService();
        var scopeFactory = CreateScopeFactory(dbName, emailService);

        int ownerUserId = 1;
        var report = PeriodicReport.Create(ownerUserId, 1, 7, 500, 100, "some_path.pdf");
        report.MarkEmailSent(); // already sent
        db.Set<PeriodicReport>().Add(report);
        await db.SaveChangesAsync();

        var optionsMonitor = new TestOptionsMonitor<PeriodicReportsOptions>(new PeriodicReportsOptions
        {
            Enabled = true,
            CheckIntervalMinutes = 1
        });

        var service = new PeriodicReportEmailRetryService(
            scopeFactory,
            optionsMonitor,
            NullLogger<PeriodicReportEmailRetryService>.Instance);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(400);

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, emailService.SendCount);
    }
}
