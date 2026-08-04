# Quality report

Single sign-off record for the repository. Evidence is recorded after each check ran against the
delivered revision.

- Verified revision: `074f2bd`
- Verified on: 2026-08-04
- Public API: <https://mauri-product-inventory-api.onrender.com>
- Public web: <https://mauri0686.github.io/product-inventory-fullstack/>

## Requirement trace

| Requirement | Evidence | Status |
|---|---|---|
| Every project targets `net9.0` | `Directory.Build.props` sets `net9.0`; Release build succeeds with `TreatWarningsAsErrors` | Met |
| Clean Architecture boundaries | Project references: Domain has none; Application owns ports; Infrastructure implements them; Api composes; Web references only Contracts | Met |
| Consistent validation and unique-name rule | DataAnnotations (400), domain invariants (422), DB check constraints + unique index; live duplicate with different casing returns 409 | Met |
| PostgreSQL, migration, exactly 100 seed products | Render deploy log shows `InitialCreate` applied; live `GET /api/products` reports `totalCount=100` (Product 001…Product 100) | Met |
| CRUD, search, filters, sort, pagination | Integration suite; live CRUD returns 201/200/409/400/200/204/404 and the seed count returns to 100 | Met |
| Structured logs and health checks | JSON console logs in Render output; live `/health/live` and `/health/ready` return `200 Healthy` | Met |
| Blazor CRUD, dashboard, 500 ms repository latency | bUnit suite; live UI dashboard reads Total 100 / Active 80 / Inventory value $2,071,312.50; `Task.Delay(500)` isolated to `HttpProductRepository` | Met |
| Loading/error/empty/confirmation states | bUnit covers the initial loader, retry, empty and inline delete confirmation | Met |
| Keyboard, focus, semantics, responsive UI | Accessible roles/labels asserted by bUnit and E2E locators; responsive CSS with per-cell `data-label` fallback | Met |
| CI, Pages, Render, end-to-end public CRUD | CI green on `074f2bd`; Pages live; Render API live; Public E2E (Playwright) green against the live demo | Met |
| No secrets, vulnerable packages, generated artifacts, or copied code | Only `.env.example` is committed; CI fails on vulnerable packages; implementation written for this repo | Met |

## Live verification (2026-08-04)

- `GET /health/live` → `200 Healthy`; `GET /health/ready` → `200 Healthy`.
- `GET /api/products?pageSize=100` → `totalCount=100`, first `Product 001`, last `Product 100`.
- `GET /api/products/summary` → `{ totalProducts: 100, activeProducts: 80, inventoryValue: 2071312.50 }`.
- CORS preflight from `https://mauri0686.github.io` → `204` with `access-control-allow-origin: https://mauri0686.github.io`.
- CRUD walkthrough (create → read → duplicate → invalid → update → delete → read) → `201/200/409/400/200/204/404`; inventory returns to 100.
- Published Blazor UI loads the 100 seeded products and the dashboard totals from the live API.

## Post-delivery enhancements (2026-08-04)

Added within the challenge constraints (no UI frameworks; Blazor authentication still intentionally
absent) and verified against the live deployment:

- Client-side column sorting with accessible `aria-sort` — verified: sorting by price descending lists Product 100 first.
- Client-side pagination of the table (10 per page) so the mobile view is not one long scroll; the dashboard totals still cover the whole collection.
- Automatic dark mode via `prefers-color-scheme` — verified: dark background `#0d1117`.
- Per-client fixed-window rate limiting on the public API — verified: a 240-request burst returned `429` responses while the Playwright flow stayed green; health checks are exempt.
- Progressive inventory load — the first batch renders immediately and the rest streams in behind a "loading more" indicator; the dashboard reads exact totals from `/summary`. Covered by a streaming bUnit test and verified live (dashboard 100 / 80 / $2,071,312.50 with all 100 rows paginated).

## Gate sign-offs

- **Product Owner** — Scope matches both challenges; no speculative features. Approved.
- **Tech Lead** — Architecture boundaries, opt-in migrations, and observability verified against the live deployment. Approved.
- **Senior Developer** — Clean build (0 warnings), locked restores, all suites green; hosted DATABASE_URL parsing covered by a regression test. Approved.
- **UI/UX** — Loading, empty, error, and confirmation states plus accessible roles/labels and responsive layout verified. Approved.
- **QA Senior** — Unit, PostgreSQL integration, bUnit, and public Playwright smoke tests green; live CRUD verified. Approved.

## Final decision

Approved. CI, both public services, and the end-to-end public CRUD smoke test are green on revision `074f2bd`.
