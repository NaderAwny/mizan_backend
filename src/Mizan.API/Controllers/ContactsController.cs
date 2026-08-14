using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mizan.Application.DTOs.Contacts;
using Mizan.Application.Interfaces;

namespace Mizan.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ContactsController : BaseController
{
    private readonly IContactService _contactService;

    public ContactsController(IContactService contactService)
    {
        _contactService = contactService;
    }

    /// <summary>POST /api/contacts — إنشاء طرف جديد</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContactRequest request, CancellationToken cancellationToken)
    {
        var response = await _contactService.CreateAsync(CurrentUserId, request, cancellationToken);
        return Created(response, "تم إضافة الطرف بنجاح");
    }

    /// <summary>GET /api/contacts?page=1&amp;pageSize=20&amp;search= — قائمة الأطراف</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _contactService.GetPagedAsync(CurrentUserId, page, pageSize, search, cancellationToken);
        return Success(response);
    }

    /// <summary>GET /api/contacts/{id} — طرف بالمعرف</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var response = await _contactService.GetByIdAsync(CurrentUserId, id, cancellationToken);
        return Success(response);
    }

    /// <summary>PUT /api/contacts/{id} — تعديل طرف</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateContactRequest request, CancellationToken cancellationToken)
    {
        var response = await _contactService.UpdateAsync(CurrentUserId, id, request, cancellationToken);
        return Success(response, "تم تعديل الطرف بنجاح");
    }

    /// <summary>DELETE /api/contacts/{id} — حذف ناعم (soft delete)</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        await _contactService.DeactivateAsync(CurrentUserId, id, cancellationToken);
        return NoContent();
    }
}
