using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/visitors")]
[Authorize]
public class VisitorsController(VisitorLogService visitorLogService, VisitorAttachmentService attachmentService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<VisitorLogResponse>>> List(
        [FromQuery] string? status, [FromQuery] int? flatId, [FromQuery] string? search)
    {
        return Ok(await visitorLogService.ListAsync(status, flatId, search));
    }

    [HttpGet("{visitorLogId:int}")]
    public async Task<ActionResult<VisitorLogResponse>> Get(int visitorLogId)
    {
        var log = await visitorLogService.GetAsync(visitorLogId);
        return log is null ? NotFound(new { message = "Visitor entry not found." }) : Ok(log);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.VisitorGateRoles)]
    public async Task<ActionResult<VisitorLogResponse>> Create(VisitorLogRequest request)
    {
        var result = await visitorLogService.CreateAsync(request);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    // PUT, not POST — VisitorManagement.md's resolved decisions explicitly spell out PUT for every
    // status-pipeline action, a deliberate deviation from Complaints' POST-based action endpoints.
    [HttpPut("{visitorLogId:int}/approve")]
    public async Task<ActionResult<VisitorLogResponse>> Approve(int visitorLogId)
    {
        var result = await visitorLogService.ApproveAsync(visitorLogId);
        if (!result.Success)
        {
            return result.Error == "Visitor entry not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPut("{visitorLogId:int}/reject")]
    public async Task<ActionResult<VisitorLogResponse>> Reject(int visitorLogId)
    {
        var result = await visitorLogService.RejectAsync(visitorLogId);
        if (!result.Success)
        {
            return result.Error == "Visitor entry not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPut("{visitorLogId:int}/check-in")]
    [Authorize(Roles = RoleNames.VisitorGateRoles)]
    public async Task<ActionResult<VisitorLogResponse>> CheckIn(int visitorLogId)
    {
        var result = await visitorLogService.CheckInAsync(visitorLogId);
        if (!result.Success)
        {
            return result.Error == "Visitor entry not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPut("{visitorLogId:int}/check-out")]
    [Authorize(Roles = RoleNames.VisitorGateRoles)]
    public async Task<ActionResult<VisitorLogResponse>> CheckOut(int visitorLogId)
    {
        var result = await visitorLogService.CheckOutAsync(visitorLogId);
        if (!result.Success)
        {
            return result.Error == "Visitor entry not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpDelete("{visitorLogId:int}")]
    [Authorize(Roles = RoleNames.VisitorGateRoles)]
    public async Task<IActionResult> Delete(int visitorLogId)
    {
        var result = await visitorLogService.DeleteAsync(visitorLogId);
        if (!result.Success)
        {
            return result.Error == "Visitor entry not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return NoContent();
    }

    [HttpPost("{visitorLogId:int}/attachments")]
    [Authorize(Roles = RoleNames.VisitorGateRoles)]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<List<VisitorAttachmentResponse>>> UploadAttachments(int visitorLogId, [FromForm] List<IFormFile> files)
    {
        var result = await attachmentService.UploadAsync(visitorLogId, files);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpGet("{visitorLogId:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> DownloadAttachment(int visitorLogId, int attachmentId)
    {
        var result = await attachmentService.GetDownloadInfoAsync(visitorLogId, attachmentId);
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
