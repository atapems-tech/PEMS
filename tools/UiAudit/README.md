# tools/UiAudit

Zero-install browser audit for PEMS (Node >= 22; works with any Chromium/Chrome/Edge, e.g. the one Playwright installs under `%LOCALAPPDATA%\ms-playwright\chromium-*\chrome-win64\chrome.exe`).
Read-only: it GETs the pages in the signed-in role's sidebar and **opens (never submits)** dropdowns, modals and offcanvases.
**Use only against a clone/test database with test accounts.** Credentials come from a private file (`CREDS`, lines `user<TAB>password`) and are never printed.

```
set CHROME=<path to chrome.exe>
set BASE=http://localhost:5199
set USERS=test-pcat-dm,test-pcat-cataloguer
set OUT=ui-audit-results
node tools/UiAudit/audit.mjs                     (English)
set LOCALE=fa && node tools/UiAudit/audit.mjs    (Persian / RTL)
set ONLY=/EngineeringDocuments,/Asset            (optional: only these paths)
```
Git Bash on Windows rewrites arguments that start with `/`; set `MSYS_NO_PATHCONV=1` when passing `ONLY`.

Findings (JSON in `OUT`): `dropdown-clipped-or-obscured`, `dropdown-offscreen`, `modal-*` / `offcanvas-*` (not opening, obscured, focus not moved inside, Escape ignored), `js-exception*`, `console-error`, `request-failed`, `http-4xx-5xx-subresource`, `page-horizontal-scroll`, `h1-count`, `duplicate-heading`, `duplicate-ids`, `unlabeled-inputs`, `shell-dropdown-obscured-or-offscreen`.

Popups are judged by real hit-testing (`document.elementFromPoint` on a 3x3 grid) and UI state is polled (`waitFor`), never fixed sleeps - on a loaded machine fixed sleeps produced false modal findings.

Limits: only pages reachable from the account's sidebar (Admin-only pages need an account with that role); row-action state loss and dependent-field behaviour need targeted scenarios (see the audit report), not the generic crawl.

Standard this tool enforces: `docs/design/PEMS_UI_ENGINEERING_STANDARD.md`.
