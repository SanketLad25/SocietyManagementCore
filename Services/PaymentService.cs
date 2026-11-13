using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

/// Payment recording (maintenance billing Phase 4, accounts-management skill). Confirmed decisions:
/// (1) a Payment row IS the receipt — no separate Receipt table/entity; (2) partial payments are
/// supported, so a bill's Status/OutstandingAmount always derive from the running sum of its
/// non-cancelled payments via RecomputeBillStatusAsync — the ONE place that math happens, so record
/// and cancel can never drift apart from each other.
public class PaymentService(SocietyDbContext db, ICurrentUserContext currentUser, AuditLogService auditLog)
{
    public async Task<List<PaymentResponse>> ListPaymentsForBillAsync(int billId)
    {
        var bill = await FindVisibleBillAsync(billId);
        if (bill is null)
        {
            return new List<PaymentResponse>();
        }

        var payments = await db.Payments
            .Where(p => p.BillId == billId && p.SocietyId == bill.SocietyId)
            .OrderByDescending(p => p.PaymentId)
            .ToListAsync();

        return await ToResponsesAsync(payments, bill.FlatId);
    }

    public async Task<PaymentResponse?> GetPaymentAsync(int paymentId)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return null;
        }

        var payment = await db.Payments.FirstOrDefaultAsync(p => p.PaymentId == paymentId && p.SocietyId == societyId);
        if (payment is null)
        {
            return null;
        }

        // Visibility of a payment is governed by the visibility of the bill it's against — same
        // Resident-vs-committee scoping as MaintenanceBillingService.GetBillAsync, deliberately
        // mirrored rather than reinvented.
        var bill = await FindVisibleBillAsync(payment.BillId);
        if (bill is null)
        {
            return null;
        }

        var responses = await ToResponsesAsync(new List<Payment> { payment }, bill.FlatId);
        return responses.FirstOrDefault();
    }

    /// BillingRoles only (enforced by the controller too). Wrapped in an explicit transaction
    /// because it touches Payment (insert), MaintenanceBill (status/outstanding update), and
    /// AuditLog in one unit — accounts-management skill §13.
    public async Task<ServiceResult<PaymentResponse>> RecordPaymentAsync(PaymentRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<PaymentResponse>.Fail("No society context for this account.");
        }

        if (request.AmountPaid <= 0)
        {
            return ServiceResult<PaymentResponse>.Fail("Amount paid must be greater than zero.");
        }

        if (!PaymentModes.IsValid(request.PaymentMode))
        {
            return ServiceResult<PaymentResponse>.Fail($"Invalid payment mode. Must be one of: {string.Join(", ", PaymentModes.All)}.");
        }

        // Tenancy check, not just a not-found check (accounts-management skill §11) — a BillId
        // from another society must fail the same friendly way as one that doesn't exist at all.
        var bill = await db.MaintenanceBills.FirstOrDefaultAsync(b => b.BillId == request.BillId && b.SocietyId == societyId);
        if (bill is null)
        {
            return ServiceResult<PaymentResponse>.Fail("Bill not found in this society.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();

        Payment payment;
        try
        {
            payment = await InsertPaymentWithReceiptNoAsync(societyId, bill, request);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return ServiceResult<PaymentResponse>.Fail("Unable to record payment. Please try again.");
        }

        await RecomputeBillStatusAsync(bill);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return ServiceResult<PaymentResponse>.Fail("Unable to record payment. Please try again.");
        }

        await auditLog.LogAsync(
            "RecordPayment", "Payment", payment.PaymentId,
            $"{payment.ReceiptNo} — {payment.AmountPaid:F2} via {payment.PaymentMode} against BillId {bill.BillId}");
        await transaction.CommitAsync();

        var response = await ToResponsesAsync(new List<Payment> { payment }, bill.FlatId);
        return ServiceResult<PaymentResponse>.Ok(response[0]);
    }

    /// BillingRoles only (enforced by the controller too). Never hard-deletes the row — flips
    /// Status to Cancelled with a required reason, then recomputes the bill the same way a fresh
    /// payment does, so a cancelled payment naturally stops counting toward "paid" (Recompute's
    /// SUM already filters WHERE Status <> Cancelled).
    public async Task<ServiceResult<PaymentResponse>> CancelPaymentAsync(int paymentId, PaymentCancelRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<PaymentResponse>.Fail("No society context for this account.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ServiceResult<PaymentResponse>.Fail("A reason is required to cancel a payment.");
        }

        var payment = await db.Payments.FirstOrDefaultAsync(p => p.PaymentId == paymentId && p.SocietyId == societyId);
        if (payment is null)
        {
            return ServiceResult<PaymentResponse>.Fail("Payment not found.");
        }

        if (payment.Status == PaymentStatuses.Cancelled)
        {
            return ServiceResult<PaymentResponse>.Fail("This payment is already cancelled.");
        }

        var bill = await db.MaintenanceBills.FirstOrDefaultAsync(b => b.BillId == payment.BillId && b.SocietyId == societyId);
        if (bill is null)
        {
            return ServiceResult<PaymentResponse>.Fail("The bill linked to this payment could not be found.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync();

        payment.Status = PaymentStatuses.Cancelled;
        payment.CancelledReason = request.Reason.Trim();
        payment.CancelledOn = DateTime.UtcNow;
        payment.CancelledBy = currentUser.UserId;

        try
        {
            // Must persist the cancellation BEFORE recomputing — RecomputeBillStatusAsync's SumAsync
            // runs a fresh query against the database, which can't see this entity's pending in-memory
            // Status change until it's actually saved. Without this, the just-cancelled payment would
            // still count toward the running total for one more recompute cycle.
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return ServiceResult<PaymentResponse>.Fail("Unable to cancel payment. Please try again.");
        }

        await RecomputeBillStatusAsync(bill);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return ServiceResult<PaymentResponse>.Fail("Unable to cancel payment. Please try again.");
        }

        await auditLog.LogAsync("CancelPayment", "Payment", payment.PaymentId, payment.CancelledReason);
        await transaction.CommitAsync();

        var response = await ToResponsesAsync(new List<Payment> { payment }, bill.FlatId);
        return ServiceResult<PaymentResponse>.Ok(response[0]);
    }

    /// The ONE place MaintenanceBill.Status/OutstandingAmount derive from recorded payments, so
    /// RecordPaymentAsync and CancelPaymentAsync can never compute two different answers.
    ///
    /// Baseline consistency: MaintenanceBillingService.GenerateAsync initializes OutstandingAmount
    /// to the bill's own total (Amount) with PenaltyAmount always 0 at generation time — so
    /// "amountDue" here is deliberately bill.Amount + bill.PenaltyAmount (this bill's own due),
    /// NOT ArrearsAmount from the flat's other unpaid bills. ArrearsAmount is a live-computed
    /// summary of *other* bills' own OutstandingAmount (MaintenanceBillingService.ToBillResponseAsync)
    /// — folding it into this bill's own OutstandingAmount would double-count the same rupee of
    /// debt on two different bill rows once both are summed across a flat's statement.
    private async Task RecomputeBillStatusAsync(MaintenanceBill bill)
    {
        var totalPaid = await db.Payments
            .Where(p => p.BillId == bill.BillId && p.Status != PaymentStatuses.Cancelled)
            .SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;

        var amountDue = (bill.Amount ?? 0m) + (bill.PenaltyAmount ?? 0m);
        var outstanding = amountDue - totalPaid;
        if (outstanding < 0)
        {
            outstanding = 0;
        }

        bill.OutstandingAmount = outstanding;
        bill.Status = totalPaid <= 0
            ? "Unpaid"
            : (outstanding <= 0 ? "Paid" : "Partially Paid");
    }

    /// Generates RCPT-{year}-{6-digit sequence} (count of this society's receipts issued this
    /// year, so far, + 1) and inserts the Payment row. Retries once on a unique-constraint
    /// collision (UX_Payment_Society_ReceiptNo) against a concurrent RecordPayment call for the
    /// same society — same defensive shape as MaintenanceBillingService.GenerateAsync's race fix
    /// (016_maintenance_billing_race_fix.sql) — rather than letting a race surface as a raw 500.
    private async Task<Payment> InsertPaymentWithReceiptNoAsync(int societyId, MaintenanceBill bill, PaymentRequest request)
    {
        const int maxAttempts = 2;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var payment = new Payment
            {
                SocietyId = societyId,
                BillId = bill.BillId,
                ReceiptNo = await GenerateNextReceiptNoAsync(societyId),
                AmountPaid = request.AmountPaid,
                PaymentDate = request.PaymentDate,
                PaymentMode = request.PaymentMode,
                TransactionId = string.IsNullOrWhiteSpace(request.TransactionId) ? null : request.TransactionId.Trim(),
                Status = PaymentStatuses.Confirmed,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            };

            db.Payments.Add(payment);

            try
            {
                await db.SaveChangesAsync();
                return payment;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                db.Entry(payment).State = EntityState.Detached;
            }
        }

        throw new DbUpdateException("Unable to generate a unique receipt number after retrying.");
    }

    private async Task<string> GenerateNextReceiptNoAsync(int societyId)
    {
        var prefix = $"RCPT-{DateTime.UtcNow.Year}-";
        var countThisYear = await db.Payments.CountAsync(p => p.SocietyId == societyId && p.ReceiptNo.StartsWith(prefix));
        return $"{prefix}{(countThisYear + 1):D6}";
    }

    /// Mirrors MaintenanceBillingService.GetBillAsync's scoping exactly: CommitteeRoles see any
    /// bill in their society; a Resident only ever sees their own flat's bills, and only once the
    /// owning cycle is Published.
    private async Task<MaintenanceBill?> FindVisibleBillAsync(int billId)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return null;
        }

        var bill = await db.MaintenanceBills.FirstOrDefaultAsync(b => b.BillId == billId && b.SocietyId == societyId);
        if (bill is null)
        {
            return null;
        }

        if (currentUser.RoleName == RoleNames.Resident)
        {
            var myFlatId = await ResolveMyFlatIdAsync();
            if (myFlatId is null || bill.FlatId != myFlatId)
            {
                return null;
            }

            var cyclePublished = await db.MaintenanceBillingCycles.AnyAsync(c => c.CycleId == bill.CycleId && c.Status == "Published");
            if (!cyclePublished)
            {
                return null;
            }
        }

        return bill;
    }

    private async Task<int?> ResolveMyFlatIdAsync()
    {
        if (currentUser.UserId is not { } userId)
        {
            return null;
        }

        return await db.UserLogins
            .Where(u => u.UserId == userId)
            .Join(db.Residents, u => u.ResidentId, r => r.ResidentId, (u, r) => r.FlatId)
            .FirstOrDefaultAsync();
    }

    private async Task<List<PaymentResponse>> ToResponsesAsync(List<Payment> payments, int? flatId)
    {
        if (payments.Count == 0)
        {
            return new List<PaymentResponse>();
        }

        var flatNo = flatId is { } id
            ? await db.Flats.Where(f => f.FlatId == id).Select(f => f.FlatNo).FirstOrDefaultAsync()
            : null;

        var userIds = payments.Where(p => p.CreatedBy.HasValue).Select(p => p.CreatedBy!.Value).Distinct().ToList();
        var displayNames = await ResolveDisplayNamesAsync(userIds);

        return payments.Select(p => new PaymentResponse
        {
            PaymentId = p.PaymentId,
            ReceiptNo = p.ReceiptNo,
            BillId = p.BillId,
            FlatNo = flatNo ?? string.Empty,
            AmountPaid = p.AmountPaid,
            PaymentDate = p.PaymentDate,
            PaymentMode = p.PaymentMode,
            TransactionId = p.TransactionId,
            Status = p.Status,
            Notes = p.Notes,
            CreatedOn = p.CreatedOn,
            CreatedByName = p.CreatedBy.HasValue ? displayNames.GetValueOrDefault(p.CreatedBy.Value, string.Empty) : string.Empty,
            CancelledReason = p.CancelledReason,
            CancelledOn = p.CancelledOn,
        }).ToList();
    }

    private async Task<Dictionary<int, string>> ResolveDisplayNamesAsync(List<int> userIds)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        var users = await (
            from u in db.UserLogins
            where userIds.Contains(u.UserId)
            join r in db.Residents on u.ResidentId equals r.ResidentId into residents
            from resident in residents.DefaultIfEmpty()
            select new { u.UserId, u.Username, ResidentFullName = resident != null ? resident.FullName : null }
        ).ToListAsync();

        return users.ToDictionary(
            u => u.UserId,
            u => !string.IsNullOrWhiteSpace(u.ResidentFullName) ? u.ResidentFullName! : u.Username);
    }
}
