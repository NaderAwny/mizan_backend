using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Mizan.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseController : ControllerBase
{
    protected Guid CurrentUserId
    {
        get
        {
            var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(claimValue) || !Guid.TryParse(claimValue, out Guid userId))
            {
                throw new Core.Exceptions.UnauthorizedException("تعذر استخراج معرف المستخدم من الرمز");
            }
            return userId;
        }
    }

    protected IActionResult Success(object? data = null, string? message = null) =>
        Ok(new { success = true, message, data });

    protected IActionResult Created(object? data = null, string? message = null) =>
        StatusCode(StatusCodes.Status201Created, new { success = true, message, data });
}
