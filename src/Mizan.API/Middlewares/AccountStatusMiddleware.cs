using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Mizan.Core.Interfaces;

namespace Mizan.API.Middlewares;

public class AccountStatusMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;

    public AccountStatusMiddleware(RequestDelegate next, IMemoryCache cache)
    {
        _next = next;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context, IUnitOfWork unitOfWork)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdClaim, out int userId))
            {
                var cacheKey = $"user_active_status_{userId}";

                if (!_cache.TryGetValue(cacheKey, out bool isActive))
                {
                    var user = await unitOfWork.Users.GetByIdAsync(userId);
                    isActive = user?.IsActive ?? false;

                    // Cache user active status for 2 minutes to protect database performance
                    _cache.Set(cacheKey, isActive, TimeSpan.FromMinutes(2));
                }

                if (!isActive)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json; charset=utf-8";

                    var response = new
                    {
                        statusCode = 403,
                        message = "تم تعطيل هذا الحساب. يرجى التواصل مع الدعم الفني"
                    };

                    await context.Response.WriteAsync(JsonSerializer.Serialize(response));
                    return;
                }
            }
        }

        await _next(context);
    }
}
