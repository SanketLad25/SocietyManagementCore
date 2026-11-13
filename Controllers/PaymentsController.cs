using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

/// Reads mirror MaintenanceBillsController's view-vs-action split (mm/accounts-management skill):
/// anyone who can see a bill (CommitteeRoles + the owning Resident, scoped server-side by
/// PaymentService) can see its payments/receipts. Recording and cancelling a payment is narrower —
/// BillingRoles (Admin+Treasurer) only, same const as billing-cycle generation/publish.
[ApiController]
[Route("api/maintenance/payments")]
[Authorize(Roles = RoleNames.MaintenanceBillViewRoles)]
public class PaymentsController(PaymentService paymentService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PaymentResponse>>> List([FromQuery] int billId)
    {
        return Ok(await paymentService.ListPaymentsForBillAsync(billId));
    }

    [HttpGet("{paymentId:int}")]
    public async Task<ActionResult<PaymentResponse>> Get(int paymentId)
    {
        var payment = await paymentService.GetPaymentAsync(paymentId);
        return payment is null ? NotFound(new { message = "Payment not found." }) : Ok(payment);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.BillingRoles)]
    public async Task<ActionResult<PaymentResponse>> Record(PaymentRequest request)
    {
        var result = await paymentService.RecordPaymentAsync(request);
        if (!result.Success)
        {
            return result.Error == "Bill not found in this society."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPost("{paymentId:int}/cancel")]
    [Authorize(Roles = RoleNames.BillingRoles)]
    public async Task<ActionResult<PaymentResponse>> Cancel(int paymentId, PaymentCancelRequest request)
    {
        var result = await paymentService.CancelPaymentAsync(paymentId, request);
        if (!result.Success)
        {
            return result.Error is "Payment not found." or "The bill linked to this payment could not be found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }
}
