---
description: Use when building a new full-stack module or feature for the Society Management System (e.g. a new CRUD area like Complaints/Parking/Notices, or extending an existing one) — encodes the project's established end-to-end shape so new modules match existing ones instead of reinventing structure.
---

# Full-stack feature development — Society Management System

Two unrelated repo trees, no shared root:
- Frontend: `c:\Common\UGIT\ReactPractice` (React + Vite, port 5173 — CORS-locked, do not let Vite drift to 5174+)
- Backend: `c:\Common\IMP\Personal\SociectyManagementCore` (ASP.NET Core, port 5124)
- DB: SQL Server, database `[Society Management]`

Build one module fully end-to-end (DB → API → UI, verified in a real browser) before starting the next. Don't scaffold multiple modules at once.

## Order of implementation

1. **DB**: new numbered migration `database/0NN_<name>.sql` in the frontend repo's `database/` folder (that's where schema lives, even though it's consumed by the backend). Append-only — never edit a shipped migration. If a table already exists with different columns than you need, `ALTER` it in place (rename/add/tighten columns) rather than dropping and recreating — watch for dependent indexes/default constraints blocking `ALTER COLUMN`; drop them first, alter, then re-add (see the sql-server skill).
2. **Backend entity + mapping**: `Models/Entities/<Name>.cs`, then Fluent API mapping in `SocietyDbContext.OnModelCreating` (`.ToTable(...)`, explicit keys/FKs) — match the DB column names/types exactly. Only add the DbSet when the module is actually being built (the DB has more tables than EF has entities by design).
3. **Tenant + audit plumbing**: if the entity is tenant-scoped data (almost everything except `Society`/`Role`), implement `ITenantScoped` and, if it needs created/modified tracking, `IAuditable`. `SocietyDbContext.SaveChangesAsync` auto-stamps `SocietyId`/`CreatedOn`/`CreatedBy`/`ModifiedOn`/`ModifiedBy` on save via `ChangeTracker` — don't hand-stamp these fields in the service, and don't overwrite `SocietyId` if it's already been explicitly set (e.g. a SuperAdmin creating a resource for a specific society).
4. **DTOs**: one `<Name>Request` shared by create *and* update unless the fields genuinely diverge (see `ResidentRequest`). Response DTOs add computed/joined fields the entity doesn't have (e.g. `IsOwnComplaint`, `categoryName` from a join).
5. **Service**: `Services/<Name>Service.cs`. All business logic here, not in the controller. Return `ServiceResult<T>` (`ServiceResult.Ok(data)` / `ServiceResult.Fail(error)`) for expected failure cases (not found, duplicate, FK conflict) instead of throwing. Catch `DbUpdateException` around inserts/deletes and translate to a friendly `ServiceResult.Fail`. Read `ICurrentUserContext` (UserId/SocietyId/RoleName/ResidentId/IsSuperAdmin) for tenant scoping and actor identity — every query filters by `SocietyId == currentUser.SocietyId` unless the caller is SuperAdmin. For guarded deletes (category still referenced by live records, etc.), block the delete and require deactivation instead — see the society-business-rules skill.
6. **Controller**: `Controllers/<Name>Controller.cs`, thin — maps `ServiceResult` to HTTP status, no logic. `[Authorize]` alone on reads; add `[Authorize(Roles = ...)]` on writes using a `RoleNames` const (extend `RoleNames.cs` rather than hand-rolling a role-list string per controller). For admin-initiated state changes (assign, status change, reset), call `AuditLogService.LogAsync` so the action is traceable.
7. **DI registration**: manually register the new service in `Program.cs` (`builder.Services.AddScoped<X>()`) — there's no repository pattern and no auto-registration/convention scanning anywhere in this codebase.
8. **Frontend API wrapper**: `src/api/<module>.js`, thin — wraps `apiGet/apiPost/apiPut/apiDelete` from `client.js`. If other UI needs to react to changes (a notification bell, a stats panel), add a `<module>:changed` custom `window` event dispatched after mutating calls and a `subscribeTo<Module>Changed` helper (see `complaints.js`'s `complaints:changed` pattern) — cheaper than polling.
9. **Frontend pages**: `src/pages/<module>/<Name>List.jsx` + `<Name>Form.jsx` (one form component handling both add and edit, keyed off whether an id is present), using `Modal` for add/edit/detail, `SelectField`/`FormField` for inputs (never a bare `<select>` — it needs the custom chevron/focus-ring styling from `dataTable.css`/`auth.css`). Gate write UI by `getSession()?.role`, matching the backend's `[Authorize(Roles=...)]` — the frontend check is UX only, not the security boundary.
10. **Wire into shell**: add the route in `App.jsx`, remove the module's key from `PLACEHOLDER_ITEMS`/add it to `REAL_PAGE_KEYS` in `dashboardNav.js` so it stops rendering as `ComingSoon`. If the module should be hidden for some roles, use `NAV_ITEMS`' per-item `roles` field.

## Verification (required before calling a phase done)

`dotnet build` the backend, then exercise the real API against the real dev DB (not mocks). Start the frontend dev server on port 5173 and drive the feature in an actual browser with Playwright — log in as each relevant role, exercise the golden path and at least one permission-boundary case (a role that should be blocked), and check `page.on('console'/'pageerror')` for zero errors. Screenshot key states. See the bug-investigation skill for the credential-reset trick when a test account's password is unknown.
