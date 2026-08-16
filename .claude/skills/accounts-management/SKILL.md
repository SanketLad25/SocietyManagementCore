---
description: Use when building or reviewing anything touching money in the Society Management System — maintenance billing, payments, receipts, refunds, expenses, invoices, or financial reports. Captures the accounting business rules this app must follow, and states plainly which of them are already enforced in code versus still design guidance for unbuilt modules.
---

# Accounts management — Society Management System

Financial correctness matters more than most modules here: a wrong balance or a lost audit trail is worse than a wrong label. Treat every rule below as a requirement, not a style preference — flag a violation as a blocker in review, don't wave it through as a nitpick.

## What's already built vs. what's still design guidance

Don't assume every rule below is already enforced — check before claiming it is:

- **Built**: Maintenance billing cycles/bills (`MaintenanceBillingCycle`, `MaintenanceBill`, `MaintenanceBillLineItem`, `Services/MaintenanceBillingService.cs`), the calculation-strategy engine (see the feature-development skill), exemptions, `Invoice` (vendor invoices for expenses — see note below), `Document`, `AuditLog`.
- **Not yet built**: any dedicated Payment/Receipt recording flow beyond the raw `Payment` table in `schema.sql` (no `PaymentService`/entity exists yet), Refunds, Journal Entries / double-entry ledger, a real Expense-management module, Bank Reconciliation. The sections below for these are **forward design guidance** for when they get built — don't reference a `RefundService` or `JournalEntryService` etc. as if it exists.
- **`Invoice` is vendor-side, not resident-side** — it has `VendorName`/`InvoiceNo`, and belongs to expense tracking (§8 below), not to `MaintenanceBill` (the resident-facing bill). Don't conflate the two when a module touches "invoices."

## 1. General rules (already largely enforced by existing plumbing)

- Never hard-delete a financial record. `ITenantScoped`/`IAuditable` entities already get non-destructive stamping for free — extend that instead of adding raw `DELETE` statements. For anything money-related, "soft delete" isn't even the ceiling: prefer an explicit **cancel-with-reason** state (see Receipts/Refunds below) over a bare `IsActive` flag, since a cancelled financial record still needs to explain *why* it was cancelled.
- `CreatedBy`/`ModifiedBy`/`CreatedOn`/`ModifiedOn` come for free from `SocietyDbContext.SaveChangesAsync`'s `IAuditable` auto-stamping (see the feature-development / society-business-rules skills) — don't hand-stamp these in a new financial service.
- Every financially-significant admin action (bill generation/publish, a future payment recording, a future refund approval) should call `AuditLogService.LogAsync`, matching the pattern already used in `MaintenanceBillingService` (`"Generate"`, `"Publish"` actions logged against `MaintenanceBillingCycle`).
- Give every transaction-like row (payment, receipt, refund, journal entry) its own unique, human-referenceable number/ID — don't rely on the DB identity column alone as the user-facing reference; generate a formatted number (e.g. per-society, per-year sequence) the way a real receipt book would.

## 2. Maintenance billing

- One bill per flat per billing period — already enforced at the **cycle** level in `MaintenanceBillingService.GenerateAsync` (`"This cycle has already been generated."` guard on `cycle.Status`). If you add a second way to generate bills (e.g. an ad-hoc single-flat bill), it must check the same way — don't duplicate the guard with different logic that could drift.
- Billing frequency (monthly/quarterly/yearly) is configured per-society via `BillingFrequency`/`MaintenanceBillingCycle` — don't hardcode a cadence.
- Manual adjustments to a generated bill need the same authorization as generating one (`RoleNames.BillingRoles` = Admin+Treasurer) and should be logged the same way — don't let an "adjustment" endpoint skip the audit trail just because it's a small change.
- Late fees / interest on arrears: `Society.InterestOnArrearsPercent` already exists as a per-society configurable rate (migration `007_society_billing_details.sql`) and `MaintenanceBill.PenaltyAmount` already exists as a column — a late-fee feature should compute into `PenaltyAmount` using this rate, not invent a new hardcoded percentage.

## 3. Receipts (not yet built — design guidance)

- Generate a receipt only after a payment is actually confirmed — never speculatively ahead of confirmation.
- Unique receipt number per receipt (see §1's numbering guidance).
- Never delete a receipt — only **cancel with a required reason**, keeping the original receipt row intact and visible in history.
- Guard against duplicate receipt generation for the same payment the same way `GenerateAsync` guards against duplicate bill generation for the same cycle — one payment, one receipt, enforced server-side, not just avoided by UI convention.

## 4. Payments (not yet built beyond the raw `Payment` table — design guidance)

- Support multiple payment modes (Cash, Cheque, UPI, Card, NEFT) — the existing `Payment.PaymentMode` column is already a free-text `VARCHAR`, so validate against a known set in the service layer rather than trusting arbitrary client input.
- Record the payment reference/transaction number where the mode has one (cheque number, UTR, etc.) — `Payment.TransactionId` already exists for this.
- If partial payments are supported, a bill's status must derive from **sum of applied payments vs. bill amount**, not from a single payment's own status — don't let the last payment recorded blindly set the bill to "Paid" without checking the running total.
- A failed/pending payment must never flip the linked bill to "Paid" — only a confirmed successful payment does. Model failure as its own `Payment.Status`, not as an absence of a row.

## 5. Outstanding dues

- Outstanding balance = unpaid bill amounts + applied penalties − any adjustments, excluding cancelled bills entirely (not just filtering them from the display — exclude them from the calculation itself).
- `MaintenanceBill.OutstandingAmount` already exists as a column set at generation time — a payment-recording feature must decrement it (or recompute it) as payments are applied, not leave it static after the bill is created.

## 6. Refunds (not yet built — design guidance)

- Require approval before a refund is finalized — model this as its own status field, not an implicit side effect of some other action.
- Store the reason for the refund as a required field, not optional.
- A refund must reference the original payment/receipt it refunds — never a bare amount with no link back.
- If refund vouchers are needed, generate them the same disciplined way as receipts (§3) — unique number, never deleted, only cancellable with a reason.

## 7. Journal entries / double-entry ledger (not yet built — undecided)

Double-entry accounting has not been adopted anywhere in this codebase yet — there's no `JournalEntry`/ledger table or service. If a future request asks for one: every entry must balance (sum of debits == sum of credits) before it's allowed to post, and account mappings must be validated against a real chart of accounts before posting, not trusted from client input. Don't retrofit double-entry bookkeeping onto `MaintenanceBill`/`Payment` as they exist today — that's a separate ledger layer sitting alongside them, not a rewrite of them.

## 8. Expense management (partially scaffolded — `Invoice` table exists, no service/entity yet)

- Every expense needs a category — mirror the existing `MaintenanceCategory` per-society pattern (see the society-business-rules skill's "categories/lookups are per-society" rule) rather than a global hardcoded expense-type enum.
- Store vendor details on the expense/invoice itself (`Invoice.VendorName` already exists) — don't require a separate vendor master table unless the product spec calls for one.
- Attach supporting documents via the existing `Document` table (already has `FlatId`/`ResidentId` FKs — extend it with an `InvoiceId` link rather than building a second attachment mechanism; compare with how `ComplaintAttachment`/`NoticeAttachment` were built as dedicated per-module tables, and pick deliberately rather than defaulting).
- Guard against duplicate expense entries the same way bill/receipt duplication is guarded — a server-side check, not a UI-only warning.

## 9. Bank reconciliation (not yet built — design guidance)

- Matching is between recorded `Payment` rows and actual bank transactions — model unmatched transactions as a flagged, reviewable state, not a silent gap.
- Once a transaction is reconciled, it should require an explicit elevated action (with its own audit log entry) to un-reconcile or modify — not be editable the same way an unreconciled one is.

## 10. Financial reports

- Exclude cancelled records by default; make "include cancelled" an explicit opt-in filter, never the default.
- Support the filters this app already models data by — society (tenant), and once they exist, building/wing and flat — following the existing `SocietyId` tenant-scoping pattern (see the society-business-rules skill), not a new ad-hoc scoping mechanism.
- A report's totals must reconcile against the underlying bill/payment rows it's summarizing — if a report and a detail list can disagree, that's a bug, not an acceptable rounding difference to shrug off.

## 11. Validation

- Amounts are non-negative except where a credit/refund is explicitly modeled as negative — validate this at the service layer, not just with a `[Range]` DTO annotation that a refund DTO would then have to bypass.
- Before processing, confirm every referenced entity (flat, resident, bill, invoice) actually exists **and belongs to the caller's society** — a not-found check without a tenancy check is an incomplete check (see the code-review skill's tenancy-leak warning).

## 12. Security

- Financial write operations need their own role check, not a reflexive `CommitteeRoles` — `RoleNames.BillingRoles` (Admin+Treasurer) already exists for billing-cycle generate/publish; extend it or add a narrower const the same deliberate way, per the society-business-rules skill's "don't default to CommitteeRoles" guidance.
- Every sensitive financial action must produce an `AuditLogService.LogAsync` entry — a financial write with no matching audit row is a gap to flag in review, not a nice-to-have.
- Any high-value adjustment or write-off should require its own explicit approval step (mirroring the Refunds approval rule in §6) rather than being just another field edit on an existing form.

## 13. Database

- Use a DB transaction for any operation that writes across more than one table (e.g. a payment insert plus a bill status update plus an audit log row) — if one part fails, none of it should persist. Wrap in an explicit `SocietyDbContext.Database.BeginTransactionAsync()` when a single `SaveChangesAsync()` call can't cover it atomically (EF Core batches one `SaveChangesAsync` into one transaction automatically — only reach for an explicit transaction when you need multiple `SaveChangesAsync` calls or raw SQL alongside EF changes in the same unit of work).
- Match the delete/alter conventions from the sql-server skill for any new migration this area needs — append-only, `ALTER` in place rather than recreate.

## 14. API

- Follow the existing `ServiceResult<T>` → HTTP status mapping (feature-development skill) for expected financial failures (insufficient authorization for a write-off, duplicate receipt, bill already generated) — don't throw for these.
- Never let a raw exception message reach the client for a financial operation — translate to a friendly `ServiceResult.Fail(...)`, matching the existing `DbUpdateException` handling convention.

## Related

feature-development (end-to-end build shape), society-business-rules (role/tenancy defaults, category-per-society pattern), sql-server (migration mechanics), code-review (tenancy-leak and ServiceResult checks to apply here too).
