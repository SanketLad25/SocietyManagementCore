---
description: Use when a design decision needs to match how this specific Society Management System already handles roles, tenancy, permissions, or module-specific business rules — e.g. "who should be able to do X", "should this be a hard delete", "should this notify everyone or just some roles". Domain/product-rule reference, not generic advice.
---

# Society Management System — domain & business rules

Multi-tenant residential society management app. One deployment serves many societies (`Society` table is the tenant root); almost every other table carries a `SocietyId` and is scoped to it.

## Roles

`SuperAdmin` (platform-level, no `SocietyId`, creates societies + each society's first Admin), `Admin`, `Chairman`, `Secretary`, `Treasurer`, `Resident`, `Security`. `RoleNames.CommitteeRoles` = Admin+Chairman+Secretary+Treasurer, used as the default "write access" role list for plain CRUD modules (Residents, Flats). **Don't assume CommitteeRoles for every module** — several modules have deliberately different splits, decided per-module against the product spec, not derived from a generic rule:

- **Complaints**: write access (assign/status/comments/resolution photos) is **Admin-only**, not all Committee roles — this was an explicit correction from a broader "Admin/Secretary/Chairman" draft. Secretary/Chairman are still **notification recipients** and **viewers**, just not writers.
- **Complaints view access is universal** — every society member (any role) can see the full complaint list with details, not just Admin/Committee. Don't gate the list page itself by role; gate only the action buttons.
- **User administration** (create/edit/reset-password/deactivate `UserLogin` rows via `AdminUsersController`) is Admin-only across every action, including password reset — stricter than CommitteeRoles.
- When adding a new module, don't default to CommitteeRoles without checking whether the module has its own explicit Admin-vs-Resident-vs-Security split in its spec — Notices, Visitors, and Parking were all called out as needing their own splits.

## Delete vs. deactivate

Never hard-delete a lookup/category row that live records may reference (e.g. `ComplaintCategory`) — block the delete with a friendly `ServiceResult.Fail` ("linked records exist") and offer deactivation (`IsActive` flag) instead. This mirrors `FlatService.DeleteAsync`'s existing guard — extend the same pattern rather than inventing a new one per module.

## Status pipelines

Where a record has a lifecycle status (e.g. `Complaint`: Open → Assigned → InProgress → Resolved → Closed), transitions are **forward-only** — enforce via an explicit order table (`ComplaintStatuses.Order`) checked in the service before accepting a status update, not by trusting the client to send a valid next state.

## Categories/lookups are per-society, not global

When a module needs a configurable category/type list (e.g. `ComplaintCategory`), default to **per-society, Admin-CRUD-able**, not a shared global static list — one society's Admin shouldn't be forced into categories another society defined, and shouldn't be able to edit another society's list. This mirrors the existing `NoticeCategory`/`MaintenanceCategory` shape.

## Notifications vs. view access are separate concerns

A role can be a notification recipient without being a writer (Secretary/Chairman get notified on new complaints but can't act on them), and view access can be broader than both (all residents can view, only Admin notified+writes). Model these as three independent checks, not one permission level.

## First-login / password rules

Admin-created users (via User Administration) get a Password+Confirm at creation and are forced through a change-password flow on first login. Only Admin can Edit a user or Reset a password; `IsActive = false` blocks login outright (checked in `AuthService.LoginAsync`, not just hidden in the UI).

## Per-flat data, not per-resident

Billing/vehicle-count style features (Parking) are modeled per-**flat**, not per-resident — a flat can have multiple two-wheelers/four-wheelers, and the billing engine already reads counts off the `Parking` table keyed by `FlatId`. Before building a "per-resident" version of something billing-adjacent, check whether it should actually be per-flat.

## Related

See the feature-development skill for how these rules get implemented end-to-end, and the code-review skill for checking a PR actually followed the role/tenancy split it claims to.
