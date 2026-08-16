using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/complaints/{complaintId:int}/attachments")]
[Authorize]
public class ComplaintAttachmentsController(ComplaintAttachmentService attachmentService) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<List<ComplaintAttachmentResponse>>> Upload(
        int complaintId, [FromForm] string kind, [FromForm] List<IFormFile> files)
    {
        var result = await attachmentService.UploadAsync(complaintId, kind, files);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpGet("{attachmentId:int}")]
    public async Task<IActionResult> Download(int complaintId, int attachmentId)
    {
        var result = await attachmentService.GetDownloadInfoAsync(complaintId, attachmentId);
        if (!result.Success)
        {
            return NotFound(new { message = result.Error });
        }

        var (physicalPath, contentType, fileName) = result.Data;
        Response.Headers[HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue("inline")
        {
            FileName = fileName,
        }.ToString();

        var stream = System.IO.File.OpenRead(physicalPath);
        return File(stream, contentType);
    }
}
