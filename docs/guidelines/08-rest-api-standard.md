# 08 - REST API standard

Status: **Accepted - 2026-10-28** (plan row #431, [ADR-0009](../../plan/adr/0009-rest-api-standard.md)). Read this chapter when you add or change anything under `/api` or any JSON endpoint.
The ADR holds the reasons; this chapter holds the rules and one good and one bad example for each. The API quality gates (#436) will check what can be checked by machine.

| # | Rule | Good | Bad |
|---|---|---|---|
| 1 | Resources are plural kebab-case nouns, no verbs, one level of nesting for ownership | `GET /api/v1/work-orders/42/attachments` | `GET /api/v1/getWorkOrderAttachments?id=42` |
| 2 | Business actions are sub-resources, never GET | `POST /api/v1/work-orders/42/actions/approve` | `GET /api/v1/work-orders/42/approve` |
| 3 | Use the verb that matches: POST create, PUT replace, PATCH partial (JSON Merge Patch), DELETE remove | `PATCH /api/v1/definitions/7` with `{ "title": { "en": "Pumps" } }` | `POST /api/v1/definitions/7/update` |
| 4 | Create returns 201 with `Location`; long work returns 202 with an operation `Location` | `201 Created`, `Location: /api/v1/definitions/8` | `200 OK` with `{ "ok": true }` |
| 5 | Status codes mean what they say: 401 not signed in, 403 not allowed, 404 not found or not visible, 409 state conflict, 422 validation | `422` with a field error list | `200 OK` with `{ "error": "..." }` |
| 6 | Writes need `If-Match` with the ETag from the read; missing 428, stale 412 | `PUT` with `If-Match: "v12"` | `PUT` without it, last writer wins |
| 7 | Creates accept `Idempotency-Key`; a replay returns the first response | `POST` twice with the same key creates one record | a double click creates two records |
| 8 | Errors are `application/problem+json` (RFC 9457) with an `errors` list (`pointer`, `code`, `messageKey`, `message`, `params`) | `{ "type": "...", "status": 422, "errors": [ { "pointer": "/title/en", "code": "required" } ] }` | a stack trace or an HTML error page |
| 9 | Lists use the one query contract: `sort`, `filter[field]`, `q`, `fields`, and paging as below | `GET /work-orders?filter[status]=Open&sort=-createdAt&page=2&pageSize=25` | `GET /work-orders?statusOpen=1&orderby=created_desc&p=2` |
| 10 | Human grids page by offset (total, jump to page; `pageSize ≤ 200`, first 10,000 rows); exports, sync and very large tables page by cursor (`limit`, opaque `cursor`, no total) | `GET /work-orders?limit=100&cursor=eyJ...` for an export | `page=5000` on a 500,000-row table |
| 11 | JSON is camelCase; enum values are domain names as strings; instants are ISO 8601 UTC, dates `yyyy-MM-dd`; numbers are never formatted; decimals are plain JSON numbers (a field needing more than about 15 significant digits is declared a string in OpenAPI) | `{ "dueDate": "2026-10-28", "amount": 1250.50, "status": "Open" }` | `{ "DueDate": "۱۴۰۵/۰۸/۰۶", "amount": "1,250.50" }` (formatted text instead of a number) |
| 12 | Stored translations are the `TranslatableText` map; message text follows `Accept-Language` | `"title": { "fa": "پمپ‌ها", "en": "Pumps" }` | a language-specific field per language |
| 13 | Every endpoint has an authorization policy and applies row-level security to lists, counts and single reads | `[Authorize(Policy = "Definitions.Edit")]` | an endpoint with no attribute |
| 14 | Browser calls: cookie plus antiforgery header on unsafe verbs; integrations: bearer token, no cookie; CORS deny by default | `X-CSRF-TOKEN` header on `PATCH` | `Access-Control-Allow-Origin: *` |
| 15 | Single-resource responses carry `allowedActions` so a dynamic UI hides what the user may not do (the server still enforces) | `"allowedActions": ["edit", "approve"]` | the UI guesses from the role name |
| 16 | Version in the path; adding optional fields is not breaking; a breaking change is a new major served next to the old with `Deprecation` and `Sunset` headers | `/api/v1` and `/api/v2` side by side | renaming a field in v1 |
| 17 | The OpenAPI 3.1 document is generated from the code, served offline at `/api/v1/openapi.json`, and a CI snapshot fails on an unapproved change | change the code, update the approved snapshot in the same PR | edit the document by hand |

Display is not data: digits and calendars shown to a user are formatted by the page (ADR-0008, `IPemsLocale`); the API always sends canonical values.

When a rule does not fit, change the ADR first (a new ADR if it reverses a decision), then this chapter, then the code.

## Building an endpoint (the walking skeleton, SCRUM-435)

The shared plumbing lives in `PEMS.Web/Api`; the pilot resource `DisciplinesApiController` (`/api/v1/disciplines`) is the example to copy.

1. Derive from `ApiControllerBase` (it is `[ApiController]`, answers in `application/problem+json`) and give the class `[Route(Prefix + "/<plural-kebab-noun>")]`.
2. Mark **every** action with `[ApiPermission("<Module>", "<Action>")]` (the permission matrix, `IPermissionService`); a test fails for an API action without it or with `[AllowAnonymous]`.
3. Keep the controller thin: map HTTP to a service in `PEMS.Application`/`PEMS.Persistence` that the pages also use. Errors come from `ApiProblem` (never an ad hoc body); a field error needs a JSON Pointer and a message key.
4. Lists use `ApiListQuery` (and `ApiFields` for `fields`); single reads set the `ETag` from the row version; PUT, PATCH and DELETE call `ApiETag.Evaluate` before writing and pass the row version to the service so the check is repeated on the database.
5. A concurrency token needs a `[Timestamp] byte[] RowVersion` on the entity (new migration).
6. Run `PEMS_GOLDEN_RECORD=1 dotnet test PEMS.Tests --filter OpenApiDocumentTests` and commit the changed `PEMS.Tests/Api/Snapshots/openapi-v1.json` in the same PR.

Known gaps, tracked in other rows: the integration bearer token (issuing and validation, #195); the Idempotency-Key store is in memory and per server (durable store with #434); cursor paging (first export endpoint); HTTP-level tests of the real pipeline need SQL Server.
