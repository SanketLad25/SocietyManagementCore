using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/knowledge-articles")]
[Authorize]
public class KnowledgeArticlesController(KnowledgeArticleService articleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<KnowledgeArticleResponse>>> List()
    {
        return Ok(await articleService.ListAsync());
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<ActionResult<KnowledgeArticleResponse>> Create(KnowledgeArticleRequest request)
    {
        var result = await articleService.CreateAsync(request);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpPut("{articleId:int}")]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<ActionResult<KnowledgeArticleResponse>> Update(int articleId, KnowledgeArticleRequest request)
    {
        var result = await articleService.UpdateAsync(articleId, request);
        if (!result.Success)
        {
            return result.Error == "Article not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }
}
