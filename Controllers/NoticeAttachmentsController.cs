using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/notices/{noticeId:int}/attachments")]
[Authorize]
public class NoticeAttachmentsController(NoticeAttachmentService attachmentService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<List<NoticeAttachmentResponse>>> Upload(int noticeId, [FromForm] List<IFormFile> files)
    {
        var result = await attachmentService.UploadAsync(noticeId, files);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpGet("{attachmentId:int}")]
    public async Task<IActionResult> Download(int noticeId, int attachmentId)
    {
        var result = await attachmentService.GetDownloadInfoAsync(noticeId, attachmentId);
        if (!result.Success)
        {
            return NotFound(new { message = result.Error });
        }

        var (physicalPath, contentType, fileName) = result.Data;
        var isPreviewable = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
            contentType == "application/pdf";

        Response.Headers[HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue(isPreviewable ? "inline" : "attachment")
        {
            FileName = fileName,
        }.ToString();

        var stream = System.IO.File.OpenRead(physicalPath);
        return File(stream, contentType);
    }

    [HttpDelete("{attachmentId:int}")]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<IActionResult> Delete(int noticeId, int attachmentId)
    {
        var result = await attachmentService.DeleteAsync(noticeId, attachmentId);
        if (!result.Success)
        {
            return NotFound(new { message = result.Error });
        }

        return NoContent();
    }
}
