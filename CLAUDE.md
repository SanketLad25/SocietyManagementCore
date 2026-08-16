# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

ASP.NET Core Web API (.NET 9, controller-based) backing a residential society management portal for **Shubhangi CHSL**. It is the server half of a two-repo full-stack app:

- **This repo** — at `c:\Common\IMP\Personal\SociectyManagementCore` (the folder name misspells "Society" — that's intentional, matches how it was created; the project/namespace itself is correctly spelled `SocietyManagementCore`)
- **Frontend** — React + Vite SPA, at `c:\Common\UGIT\ReactPractice`
- **Database** — SQL Server, database name literally `Society Management` (note the space — always bracket/quote it: `[Society Management]`). Full schema (19 tables) lives in the frontend repo at `ReactPractice/database/schema.sql` + `ReactPractice/database/002_document_invoice_auditlog.sql`, since that's where it was first authored — check there for column names/types before assuming an entity shape.

The two repos live in unrelated directory trees with no shared root; they're developed in lockstep but must be opened/referenced independently.

## Commands

```bash
dotnet build                          # build
dotnet run --launch-profile http      # run on http://localhost:5124 (matches frontend's VITE_API_BASE_URL and CORS origin)
dotnet run --launch-profile https     # run on https://localhost:7262 + http://localhost:5124
```

No test project exists yet. Also runnable directly from Visual Studio (there's a `.vs/` folder and `SocietyManagementCore.csproj.user`) — if a build fails with "the process cannot access the file... .exe" or a port is "already in use," it's very likely the app is already running under the VS debugger; stop debugging there before rebuilding/running from the CLI.

Only ever one process should hold port 5124 at a time — check before starting another (`Get-Process -Name SocietyManagementCore` / `Get-NetTCPConnection -LocalPort 5124` in PowerShell) rather than assuming a previous run was cleanly stopped.

## Architecture

### Multi-tenancy

This is a multi-tenant app: one deployment serves many societies. `Society` (`Models/Entities/Society.cs`) is the tenant root; almost every other entity carries a `SocietyId` and implements `ITenantScoped` (`Models/Entities/IAuditable.cs` — `ITenantScoped : IAuditable`, adds `int SocietyId`).

- `Services/ICurrentUserContext.cs` / `CurrentUserContext.cs` — the one place every service reads "who is making this request" from, backed by JWT claims: `UserId`, `RoleName`, `SocietyId` (nullable — null means SuperAdmin), `ResidentId` (nullable — set only when the login is linked to a `Resident`), `IsSuperAdmin`. Registered scoped in `Program.cs`. Don't re-parse `ClaimsPrincipal` in a service; inject this instead.
- `Data/SocietyDbContext.SaveChangesAsync` (override) walks `ChangeTracker.Entries<IAuditable>()` and auto-stamps `CreatedOn`/`CreatedBy`/`ModifiedOn`/`ModifiedBy` on Added/Modified entries, and sets `SocietyId` from the current user **only if it's still the default `0`** — so a SuperAdmin explicitly creating a resource for a specific society (e.g. that society's first Admin) isn't overridden. Services should not hand-stamp any of these fields.
- Every service query on an `ITenantScoped` entity must filter by `SocietyId == currentUser.SocietyId`. A missing filter is a cross-tenant data leak — the most severe class of bug in this app.
- Login (`LoginRequest`: `SocietyName` (optional) + `Username` + `Password`) resolves `SocietyName` to a `SocietyId` and matches `UserLogin` on `(SocietyId, Username)`; a blank `SocietyName` matches only `SocietyId IS NULL` rows — the SuperAdmin path. There is no self-registration — `AuthController` only exposes login. New users are created by an Admin (`AdminUsersController`, within their own society) or, for a society's first Admin, by a SuperAdmin (`SocietiesController`).

### Data access: EF Core mapped onto a hand-authored schema

`Data/SocietyDbContext.cs` maps EF entities onto tables that were created by raw SQL (see `database/schema.sql` + numbered migrations in the frontend repo's `database/` folder), **not** onto tables EF generated. There are no EF migrations and none should be added for tables that already exist — `OnModelCreating` uses Fluent API (`.ToTable(...)`, explicit `HasKey`/`HasForeignKey`) to match the existing schema exactly. The DB has more tables than EF has entities by design — add a new entity/DbSet only when that table's module is actually being built, matching column names/types from the schema/migration file exactly.

`Data/DbSeeder.cs` seeds all roles from `RoleNames.All` into the `Role` table on startup if empty, plus a bootstrap SuperAdmin login (`SeedSuperAdminAsync`). `Data/MaintenanceReferenceSeeder.cs` / `NoticeReferenceSeeder.cs` / `ComplaintReferenceSeeder.cs` backfill per-society reference/lookup data for existing societies on every startup — follow this same "backfill for existing tenants" pattern if a new module introduces its own per-society lookup table.

### Auth

- `Services/AuthService.cs` — login only (no registration). Password hash (bcrypt via `BCrypt.Net-Next`) lives only on `UserLogin.PasswordHash`; `Resident.PasswordHash` is deliberately left `NULL` and is not read anywhere — `UserLogin` is the single source of truth for credentials, `Resident` is profile data only. `UserLogin.IsActive = false` blocks login outright, checked here (not just hidden in the UI).
- `Services/JwtTokenService.cs` — issues the JWT; claims include `sub`/`NameIdentifier` (UserId), `Name` (username), `Role`, a `societyId` claim when the user has one, and a `residentId` claim when the login is linked to a resident. `[Authorize(Roles = "...")]` works against these out of the box since `ClaimTypes.Role` is the default role claim type.
- `Options/JwtOptions.cs` bound from the `Jwt` config section. The signing key in `appsettings.Development.json` is a real generated secret checked in for local dev only — rotate it (and move to user-secrets/an env var) before this ever ships or the repo goes into shared source control.
- `Program.cs` intentionally skips `UseHttpsRedirection()` in Development so the frontend can call the API over plain HTTP without dealing with the self-signed dev cert. Don't "fix" this by re-enabling it without also updating the frontend's `.env` and confirming CORS still lines up.
- CORS is locked to a single hardcoded origin, `http://localhost:5173` (the frontend's exact dev port) — update this if the frontend ever runs elsewhere.

### Roles & authorization pattern for CRUD modules

`Models/Entities/RoleNames.cs`: `SuperAdmin` (platform-level, not society-assignable), `Admin`, `Chairman`, `Secretary`, `Treasurer`, `Resident`, `Security`. Several named role-list consts exist beyond the general `CommitteeRoles` (`Admin,Chairman,Secretary,Treasurer`) — `BillingRoles` (Admin+Treasurer, narrower — billing-cycle generation/publish only), `NoticeManagerRoles` (Admin+Secretary+Chairman, excludes Treasurer). User administration (create/edit/reset-password, `AdminUsersController`) is Admin-only, on every action including password reset. **Don't default a new module to `CommitteeRoles`** — check whether it needs its own const first; several modules (Complaints, Notices, billing actions) deliberately have narrower or different splits than the general committee set, per their own product spec.

Established with the Residents module (`Controllers/ResidentsController.cs`, `Services/ResidentService.cs`) — follow this same shape for every module:

- Read endpoints (`GET`): `[Authorize]` only, unless the module's spec calls for universal view access regardless of role (e.g. Complaints — every society member can view).
- Write endpoints (`POST`/`PUT`/`DELETE`): `[Authorize(Roles = RoleNames.<SomeConst>)]` — pick the const that actually matches the module's spec rather than reaching for `CommitteeRoles` by default.
- Business logic lives in a `Services/<Module>Service.cs`, not the controller. Return `Models/Dtos/ServiceResult<T>` (`ServiceResult.Ok(data)` / `ServiceResult.Fail(error)`) from service methods rather than throwing for expected failure cases (not-found, duplicate, FK conflict) — the controller maps `Success`/`Error` to the right HTTP status. This is the reusable result type going forward; `AuthResult` predates it and is intentionally left as its own type rather than retrofitted.
- Catch `DbUpdateException` around inserts/deletes and translate to a friendly `ServiceResult.Fail(...)` message (e.g. FK violations on delete, invalid foreign key on create) instead of letting a raw 500/SQL error reach the client.
- DTOs are one request shape shared by create *and* update where the fields are identical (see `ResidentRequest`) — don't split into separate Create/Update DTOs unless they actually diverge.
- Delete on a lookup/category row that live records may reference should block with a `ServiceResult.Fail` and require deactivation (`IsActive`) instead of a hard delete — see `FlatService.DeleteAsync` / `ComplaintCategoryService`.
- Admin-initiated state changes (assign, status change, password reset) call `Services/AuditLogService.cs` (`LogAsync`) so the action is traceable.
- New services are registered manually in `Program.cs` (`builder.Services.AddScoped<X>()`) — there is no repository pattern and no auto-registration/convention scanning anywhere in this codebase.

### Maintenance charge calculation

`Services/Calculation/` implements a strategy pattern (`ICalculationStrategy`, one implementation per `MaintenanceChargeRule` calculation method: `FixedAmountStrategy`, `PerSqFtStrategy`, `PerUnitStrategy`, `SlabTieredStrategy`, `PercentageOfCategoryStrategy`, `FormulaStrategy`), selected at runtime via `CalculationStrategyFactory`. `MaintenanceTargetResolver` resolves which flats/residents a given charge rule applies to (whole society, a `FlatGroup`, exemptions via `MaintenanceExemptionService`). `MaintenanceBillingService` orchestrates a billing cycle end-to-end. Add a new calculation method by adding a new `ICalculationStrategy` implementation and registering it in `Program.cs`, not by branching inside an existing strategy.

### What's built so far

Multi-tenancy foundation (Societies, SuperAdmin bootstrap, Admin-managed users), Residents, Flats (+ `FlatGroup`s), Maintenance (categories, charge rules, exemptions, billing cycles/bills, amenities + subscriptions), Notices (categories, attachments, per-resident read status — `INoticeBroadcastService` is currently a no-op stub, ready for a real push/email implementation later), Parking (+ parking types), and Complaints (categories, attachments, per-recipient notifications, status pipeline, stats). Each module was built one at a time, fully wired end-to-end (DB → API → frontend pages) and verified in a real browser before moving to the next — keep following that discipline rather than scaffolding multiple modules up front. Reports and Visitors are not yet built.
