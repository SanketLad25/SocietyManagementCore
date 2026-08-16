using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/events/categories")]
[Authorize]
public class EventCategoriesController(EventCategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<EventCategoryResponse>>> List()
    {
        return Ok(await categoryService.ListAsync());
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<ActionResult<EventCategoryResponse>> Create(EventCategoryRequest request)
    {
        var result = await categoryService.CreateAsync(request);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpPut("{categoryId:int}")]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<ActionResult<EventCategoryResponse>> Update(int categoryId, EventCategoryRequest request)
    {
        var result = await categoryService.UpdateAsync(categoryId, request);
        if (!result.Success)
        {
            return result.Error == "Category not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpDelete("{categoryId:int}")]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<IActionResult> Delete(int categoryId)
    {
        var result = await categoryService.DeleteAsync(categoryId);
        if (!result.Success)
        {
            return result.Error == "Category not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return NoContent();
    }
}
