---
description: Use when reviewing a diff, PR, or just-written code in either repo of the Society Management System — checks against this project's specific conventions (tenancy, ServiceResult, role gating, styling) rather than generic best practices.
---

# Code review — Society Management System

Review against what THIS codebase actually does, not generic style. Flag deviations from established patterns as bugs, not preferences — they're inconsistencies with working code elsewhere in the same repo.

## Backend checklist

- **Tenancy**: does every query on a `ITenantScoped` entity filter by `SocietyId == currentUser.SocietyId`? A missing filter is a cross-tenant data leak, the most severe class of bug in this app — treat it as a blocker, not a nit.
- **Audit stamping**: is `SocietyId`/`CreatedOn`/`CreatedBy`/`ModifiedOn`/`ModifiedBy` being set by hand in a service method? It shouldn't be — `SocietyDbContext.SaveChangesAsync` stamps these automatically via `ITenantScoped`/`IAuditable`. Manual stamping usually means the entity is missing the interface, or someone copy-pasted from before the interface existed.
- **Role gating matches the spec, not a default**: don't accept `[Authorize(Roles = RoleNames.CommitteeRoles)]` on a module (Complaints, Notices, Visitors, Parking) that has a documented Admin-only or custom split — check the society-business-rules skill for the module before approving. Also check the read endpoint isn't accidentally role-gated when the module is meant to have universal view access.
- **ServiceResult, not exceptions, for expected failures**: not-found/duplicate/FK-conflict should return `ServiceResult.Fail(...)`, not throw. A thrown exception for an expected case means the controller can't map it to a clean 4xx and the client gets a raw 500.
- **`DbUpdateException` handling** around inserts/deletes that touch FKs — is it caught and translated, or will a constraint violation leak a raw SQL error to the client?
- **DI registration**: new service actually added to `Program.cs`? Easy to forget — the app builds fine and fails at runtime with a `InvalidOperationException` on first request to the new controller.
- **DTO shape**: is a separate Create/Update DTO introduced when the fields are actually identical to `<Name>Request`? That's an unnecessary split — flag it.
- **Delete guards**: does a delete on a referenced lookup/category actually block with a friendly message, or will it either 500 on FK violation or silently orphan child rows?
- **Forward-only status transitions**: if the entity has a status pipeline, is the next-status validated server-side against an explicit order, or trusted from client input?

## Frontend checklist

- **No bare `<select>`** — must use `SelectField`/the `.field-control select` / `.table-search select` styling from `dataTable.css`/`auth.css`. A native-looking dropdown in a screenshot is a real regression to flag, not necessarily a caching artifact — verify the class is actually applied before dismissing it (see the bug-investigation skill).
- **Role gating mirrors the backend**: `getSession()?.role` checks in the component should match the corresponding controller's `[Authorize(Roles=...)]` — a mismatch means either a button that 403s when clicked, or a hidden capability the backend already allows.
- **Modal lifecycle**: does an action inside a detail modal (assign/status/comment/etc.) close and reload the whole list, or refresh the modal's own data in place? Closing-and-reloading is a UX regression once a modal has more than one sequential action — refetch just that record instead (see `ComplaintList.jsx`'s `handleDetailChanged` for the fixed pattern).
- **`<module>.js` API wrapper stays thin** — no business logic, just `apiGet/apiPost/apiPut/apiDelete` calls plus (if needed) a `<module>:changed` event dispatch. Logic creeping into the API layer instead of a page/component is a smell.
- **New module wired into the shell correctly**: route added in `App.jsx`, key moved from `PLACEHOLDER_ITEMS` to `REAL_PAGE_KEYS`, not just a route added while `ComingSoon` still renders because the nav config wasn't updated.

## Cross-cutting

- **Migration append-only**: is a new migration file being added, or is an already-shipped `database/0NN_*.sql` being edited in place? The latter is a blocker — anyone who already ran it won't see the new change.
- Reject fixes that patch a symptom without checking whether the same bug exists in sibling modules built from the same template (e.g. a missing tenancy filter found in one service is worth grepping for across all services).
