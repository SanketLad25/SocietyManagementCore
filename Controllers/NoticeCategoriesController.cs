using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/notices/categories")]
[Authorize]
public class NoticeCategoriesController(NoticeCategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<NoticeCategoryResponse>>> List()
    {
        return Ok(await categoryService.ListAsync());
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<ActionResult<NoticeCategoryResponse>> Create(NoticeCategoryRequest request)
    {
        var result = await categoryService.CreateAsync(request);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpPut("{categoryId:int}")]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<ActionResult<NoticeCategoryResponse>> Update(int categoryId, NoticeCategoryRequest request)
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
}
