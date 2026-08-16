---
description: Use when writing or troubleshooting SQL Server migrations, schema changes, or direct DB queries against the Society Management database — covers this project's migration conventions and the specific ALTER/constraint pitfalls already hit in this DB.
---

# SQL Server — Society Management database

Database name is literally `Society Management` (with a space) — always bracket-quote it: `[Society Management]`. Schema lives in the **frontend** repo (`ReactPractice/database/`), not the backend, even though the backend is what consumes it via EF Core Fluent mappings.

## Migration conventions

- `database/schema.sql` is the original hand-authored DDL (19 tables). New tables/columns get their own numbered file, `database/0NN_<name>.sql`, in strictly increasing order — **append-only**, never edit a shipped migration.
- EF Core has no migrations of its own here — `SocietyDbContext.OnModelCreating` maps entities onto tables created by raw SQL. Only add EF entities for tables that already exist in the SQL schema; don't let EF generate schema.
- If a migration needs to modify a table that already exists with different columns than the new module needs (common when a table was stubbed early with a rough shape), **ALTER in place** — rename/add/tighten columns — rather than dropping and recreating. This preserves existing data and matches how `010_complaints.sql` reshaped the pre-existing `Complaint` table.

## `ALTER COLUMN` blocked by dependent objects

The single most common failure when tightening or renaming a column on an existing table: a dependent index or default constraint blocks the `ALTER`. SQL Server error will name the object. Fix sequence:

1. `DROP INDEX` any index referencing the column (e.g. `IX_Complaint_ResidentId`).
2. `ALTER TABLE ... DROP CONSTRAINT` any default constraint on the column (e.g. `DF_Complaint_Status`) — find its generated name via `sys.default_constraints` if not hand-named.
3. Do the rename (`sp_rename`) / `ALTER COLUMN` / tighten to `NOT NULL`.
4. Re-add the index and default constraint.

Don't try to force through with `WITH (DROP_EXISTING = ...)` tricks or skip straight to recreating the table — the drop/alter/re-add sequence is simpler and matches what's already been done successfully in this DB.

## Backfilling before tightening `NOT NULL`

When adding a `NOT NULL` column to a table with existing rows (e.g. `SocietyId` during the multi-tenant conversion), always: add the column nullable → `UPDATE` existing rows to a real backfill value → then `ALTER COLUMN ... NOT NULL`. Adding `NOT NULL` directly against a populated table fails outright.

## Running ad-hoc queries / direct fixes against the dev DB

Use `sqlcmd`. If a bare `UPDATE`/`ALTER` fails with a `QUOTED_IDENTIFIER` error, this DB has indexed views or filtered indexes that require it — prefix the session with `SET QUOTED_IDENTIFIER ON;` before the statement.

## Related

See the bug-investigation skill for the credential-reset recipe (generating a bcrypt hash and `UPDATE`-ing `UserLogin.PasswordHash` directly) — it depends on the QUOTED_IDENTIFIER fix above. See the feature-development skill for where a new migration fits in the overall build order.
