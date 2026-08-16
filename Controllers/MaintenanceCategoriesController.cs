using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/maintenance/categories")]
[Authorize]
public class MaintenanceCategoriesController(MaintenanceCategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MaintenanceCategoryResponse>>> List()
    {
        return Ok(await categoryService.ListAsync());
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<MaintenanceCategoryResponse>> Create(MaintenanceCategoryRequest request)
    {
        var result = await categoryService.CreateAsync(request);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpPut("{categoryId:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<MaintenanceCategoryResponse>> Update(int categoryId, MaintenanceCategoryRequest request)
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
