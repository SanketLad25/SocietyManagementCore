---
description: Use when investigating a reported bug or unexpected behavior in the Society Management System — the verify-before-theorizing discipline and the specific unblocking tricks (test credential resets, CORS-port issue, console-error capture) already needed repeatedly in this project.
---

# Bug investigation — Society Management System

## Verify empirically before theorizing

When a user reports something that looks wrong from a screenshot or description (e.g. "this dropdown looks native/unstyled"), don't conclude the cause from reading the CSS alone — load the actual live page in a real browser and check the computed state. In this project, a dropdown that looked native in a screenshot turned out to have the correct custom styling applied when checked live; the most likely explanation was a stale browser cache on the reporter's side, not a real regression. Report what you directly observed ("verified live, styling is applied — likely a stale cache, try a hard refresh") rather than asserting a root cause you didn't confirm firsthand. If the user's screenshot and your live check genuinely disagree, say so explicitly instead of picking one silently.

## Resetting a test account's password (do this often, don't improvise each time)

Test accounts (`mtsresident`, `mtssecurity`, `mtstreasurer`, `mtsadmin`, etc.) frequently have unknown or mismatched bcrypt hashes when you need to log in as them for verification. Fastest fix, don't waste time guessing passwords or trying to trigger a self-service reset:

1. Generate a bcrypt hash for a known password (`BCrypt.Net-Next` via a throwaway C# snippet, or any bcrypt CLI) — cost factor matching what `AuthService` uses.
2. `UPDATE UserLogin SET PasswordHash = '<hash>' WHERE Username = '<test-account>'` via `sqlcmd` against the dev DB.
3. If the bare `UPDATE` fails with a `QUOTED_IDENTIFIER` error, prefix with `SET QUOTED_IDENTIFIER ON;` first — this DB has indexed views/filtered indexes that require it (see the sql-server skill).

This is the single most-repeated unblocking move in this project's verification workflow — reach for it immediately rather than trying other logins or asking the user for a password.

## CORS / port drift

If the frontend dev server isn't on port 5173 (Vite auto-incremented to 5174+ because something else was already on 5173), every API call fails with a generic "unable to reach the server" — this presents as a network error in `fetch`, not a CORS error, so it's easy to misdiagnose as a backend-down issue. Kill stray Vite processes and restart on 5173 rather than changing the backend's CORS origin.

## Browser-based verification workflow

Use Playwright (via a scratchpad `.mjs` script run through Bash, backgrounded if it'll take a while). Always:
- Register `page.on('pageerror', ...)` and `page.on('console', msg => msg.type() === 'error' && ...)` and report the count/content at the end — zero console errors is part of "verified," not optional.
- Register `page.on('dialog', dialog => dialog.accept())` if the flow uses `confirm()`/`alert()`.
- Screenshot key states to a scratchpad shots folder — useful for confirming layout, not just absence of errors.
- Test the actual permission boundary, not just the golden path: log in as a role that should be blocked from an action and confirm the button is absent/the API 403s, not just that the allowed role can do the thing.

## Client-side validation false alarms

If a test script's own form submission silently does nothing (e.g. a modal that won't close), check whether a required field was skipped in the script before assuming an app bug — client-side validation blocking submission looks identical to a broken submit handler from the outside.

## Related

See the sql-server skill for the underlying constraint/QUOTED_IDENTIFIER mechanics, and the feature-development skill for what "verified" means before a phase is called done.
