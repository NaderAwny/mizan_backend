using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using Mizan.API.Middlewares;
using Mizan.Application.Interfaces;
using Mizan.Application.Services;
using Mizan.Core.Interfaces;
using Mizan.Infrastructure.Persistence;
using Mizan.Infrastructure.Persistence.Repositories;
using Mizan.Infrastructure.Services.Auth;
using Mizan.Infrastructure.Services.Email;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// 1. Controller & Validation Response Configuration
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var firstError = context.ModelState
                .Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault() ?? "بيانات الطلب غير صالحة";

            return new BadRequestObjectResult(new
            {
                statusCode = 400,
                message = firstError
            });
        };
    });

// 2. Caching
builder.Services.AddMemoryCache();

// 3. Database Context
if (!builder.Environment.IsEnvironment("Testing"))
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<MizanDbContext>(options =>
    {
        options.UseSqlServer(connectionString);
    });
}

// 4. Dependency Injection - Core & Infrastructure Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IShopRepository, ShopRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IOtpCodeRepository, OtpCodeRepository>();
builder.Services.AddScoped<IContactRepository, ContactRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IInstallmentRepository, InstallmentRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IInstallmentReminderLogRepository, InstallmentReminderLogRepository>();
builder.Services.AddScoped<IPeriodicReportRepository, PeriodicReportRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// 5. Dependency Injection - Application Services & Email Configuration
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IPeriodicReportService, PeriodicReportService>();
builder.Services.AddScoped<IVoiceNoteService, VoiceNoteService>();
builder.Services.AddScoped<IReportPdfGenerator, Mizan.Infrastructure.Services.Reports.ReportPdfGenerator>();
builder.Services.AddScoped<IReminderScanner, ReminderScanner>();
builder.Services.AddSingleton<IJwtProvider, JwtProvider>();

var emailOptions = builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
if (!builder.Environment.IsEnvironment("Testing"))
{
    if (!emailOptions.UseMockInDevelopment && string.IsNullOrWhiteSpace(emailOptions.ApiKey))
    {
        throw new InvalidOperationException(
            "FATAL CONFIGURATION ERROR: SendGrid API Key must be configured via user-secrets (Email:ApiKey) or environment variable (Email__ApiKey) when UseMockInDevelopment is false.");
    }
}

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.AddScoped<SendGrid.ISendGridClient>(sp =>
{
    var emailOpts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailOptions>>().Value;
    return new SendGrid.SendGridClient(emailOpts.ApiKey ?? string.Empty);
});
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.Configure<Mizan.Application.DTOs.Notifications.RemindersOptions>(builder.Configuration.GetSection(Mizan.Application.DTOs.Notifications.RemindersOptions.SectionName));
builder.Services.Configure<Mizan.Application.DTOs.Reports.PeriodicReportsOptions>(builder.Configuration.GetSection(Mizan.Application.DTOs.Reports.PeriodicReportsOptions.SectionName));
builder.Services.AddHostedService<Mizan.Infrastructure.BackgroundServices.ReminderCheckService>();

// 6. JWT Authentication & Strict Key Validation
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
const string compromisedLeakedKey = "MizanSecretSuperKeyForJwtSigning_MustBeAtLeast32BytesLong_2026!";

if (!builder.Environment.IsEnvironment("Testing"))
{
    if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey) ||
        Encoding.UTF8.GetByteCount(jwtOptions.SecretKey) < 32 ||
        jwtOptions.SecretKey == compromisedLeakedKey)
    {
        throw new InvalidOperationException(
            "FATAL SECURITY ERROR: A secure JWT secret key (minimum 32 bytes) must be configured via user-secrets or environment variables. " +
            "The leaked default key and empty keys are strictly rejected.");
    }
}
else
{
    if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey) || Encoding.UTF8.GetByteCount(jwtOptions.SecretKey) < 32)
    {
        jwtOptions.SecretKey = "TestEnvironmentSecretKey_MustBeAtLeast32BytesLong_ForTestingOnly!";
    }
}

builder.Services.Configure<JwtOptions>(options =>
{
    options.SecretKey = jwtOptions.SecretKey;
    options.Issuer = jwtOptions.Issuer;
    options.Audience = jwtOptions.Audience;
    options.AccessTokenExpirationDays = jwtOptions.AccessTokenExpirationDays;
    options.RefreshTokenExpirationDays = jwtOptions.RefreshTokenExpirationDays;
});

var key = Encoding.UTF8.GetBytes(jwtOptions.SecretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing");
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtOptions.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// 7. Rate Limiting (10 requests / minute on Auth endpoints, 100 requests / minute on General endpoints)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("AuthPolicy", opt =>
    {
        opt.PermitLimit = builder.Environment.IsEnvironment("Testing") ? 1000 : 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("GeneralPolicy", opt =>
    {
        opt.PermitLimit = builder.Environment.IsEnvironment("Testing") ? 2000 : 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});

// 8. CORS Policies (Permissive in Dev, Restricted in Production)
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentCors", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    options.AddPolicy("ProductionCors", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

// 9. Swagger / OpenAPI with JWT Bearer Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Mizan API — ميزان",
        Version = "v1",
        Description = "API توثيق وإدارة الديون والمبيعات والأقساط لتطبيق ميزان"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "أدخل رمز JWT token بهذا الشكل: Bearer {your token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Ensure database migrations are applied in local development
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
    dbContext.Database.Migrate();
}

// 10. Middleware Pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!app.Environment.IsEnvironment("Testing") && !app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Mizan API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors(app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing") ? "DevelopmentCors" : "ProductionCors");
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<AccountStatusMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
