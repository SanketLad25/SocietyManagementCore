using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/complaints")]
[Authorize]
public class ComplaintsController(ComplaintService complaintService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ComplaintResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] int? categoryId,
        [FromQuery] string? priority,
        [FromQuery] string? search)
    {
        return Ok(await complaintService.ListAsync(status, categoryId, priority, search));
    }

    [HttpGet("stats")]
    public async Task<ActionResult<ComplaintStatsResponse>> Stats()
    {
        return Ok(await complaintService.GetStatsAsync());
    }

    [HttpGet("{complaintId:int}")]
    public async Task<ActionResult<ComplaintResponse>> Get(int complaintId)
    {
        var complaint = await complaintService.GetAsync(complaintId);
        return complaint is null ? NotFound(new { message = "Complaint not found." }) : Ok(complaint);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Resident)]
    public async Task<ActionResult<ComplaintResponse>> Create(ComplaintRequest request)
    {
        var result = await complaintService.CreateAsync(request);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpPost("{complaintId:int}/assign")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ComplaintResponse>> Assign(int complaintId, ComplaintAssignRequest request)
    {
        var result = await complaintService.AssignAsync(complaintId, request);
        if (!result.Success)
        {
            return result.Error == "Complaint not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPost("{complaintId:int}/status")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ComplaintResponse>> UpdateStatus(int complaintId, ComplaintStatusUpdateRequest request)
    {
        var result = await complaintService.UpdateStatusAsync(complaintId, request);
        if (!result.Success)
        {
            return result.Error == "Complaint not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPost("{complaintId:int}/comments")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ComplaintResponse>> AddComment(int complaintId, ComplaintCommentRequest request)
    {
        var result = await complaintService.AddCommentAsync(complaintId, request);
        if (!result.Success)
        {
            return result.Error == "Complaint not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPost("{complaintId:int}/confirm-resolution")]
    [Authorize(Roles = RoleNames.Resident)]
    public async Task<ActionResult<ComplaintResponse>> ConfirmResolution(int complaintId)
    {
        var result = await complaintService.ConfirmResolutionAsync(complaintId);
        if (!result.Success)
        {
            return result.Error == "Complaint not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }
}
