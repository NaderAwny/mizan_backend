using System.Security.Claims;
using System.Text.Json;
using Mizan.Application.Interfaces;
using Mizan.Core.Interfaces;

namespace Mizan.API.Middlewares;

public class AccountStatusMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IUserStatusCache _statusCache;

    public AccountStatusMiddleware(RequestDelegate next, IUserStatusCache statusCache)
    {
        _next = next;
        _statusCache = statusCache;
    }

    public async Task InvokeAsync(HttpContext context, IUnitOfWork unitOfWork)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(userIdClaim, out Guid userId))
            {
                var isActive = await _statusCache.GetOrSetUserActiveStatusAsync(userId, async id =>
                {
                    var user = await unitOfWork.Users.GetByIdAsync(id);
                    return user?.IsActive ?? false;
                });

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
