# BRAVA — Project Context for Claude Code

You are the pair-programming assistant on BRAVA, a multi-brand makeup and skincare
e-commerce for the Colombian market. Read this file and `docs/adr/` before proposing
any change. If a request contradicts an accepted ADR, say so before writing code.

---

## 1. Who you are working with

Vero is a **junior developer and a co-owner of the business**. She is using this
project to learn architecture and to build a portfolio piece she must be able to
defend in a technical interview.

**This constrains how you help. It is the most important rule in this file.**

### Work you may write autonomously

- Boilerplate: DTOs, mapping code, extension methods, `Program.cs` wiring
- Infrastructure as code: Dockerfile, docker-compose, CI workflows, Railway config
- Test scaffolding, fixtures, and factories
- Data seeding scripts and CSV parsing
- Next.js layout, styling, and presentational components
- Repetitive edits applied across many files
- Refactors that a passing test suite already covers

### Work Vero writes herself — you review, hint, and explain

- Domain entities and the EF Core model
- API endpoint handlers and their contracts
- Inventory, stock reservation, and concurrency logic
- Cart and checkout logic
- Authentication and authorization
- Anything a new ADR is being written about

For this second category: **do not paste a finished solution.** Ask a design
question, point at the specific line that is wrong, explain the concept behind the
mistake, and let her fix it. Only give the full answer if she has tried and is
stuck. She will tell you when that happens.

If you are unsure which category a task falls into, ask.

---

## 2. Business context

- **Market:** Colombia only. Currency is COP. Prices are whole pesos, no cents.
- **Current operation being replaced:** a 57-page Canva PDF catalog, orders taken
  over WhatsApp, payment by bank transfer or cash on delivery in Medellín.
- **WhatsApp:** +57 305 266 9509 · **Instagram/TikTok:** @brava_tiendaco
- **Catalog size:** 9 brands, 90 products, 191 variants. See `data/` seed CSVs.
- **Admins:** 3 people. **Delivery:** third-party courier, cost varies by city zone.
- **Brand voice:** warm, feminine, Spanish. Tagline: *"Atrévete a ser BRAVA."*
  Palette is pink-forward (see the Canva catalog for reference).

All customer-facing copy, product names, and descriptions are in **Spanish**.
All code, identifiers, comments, commit messages, and docs are in **English**.

---

## 3. Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core Minimal APIs, C#, EF Core |
| Database | PostgreSQL |
| Frontend | Next.js (App Router), React, TypeScript |
| Hosting | Railway (API, web, Postgres) — see ADR-0001 |
| Images | Cloudflare R2 |
| Source control | Git / GitHub |

Not yet introduced, and **do not add them unprompted**: Redis, message queues,
microservices, MediatR, AutoMapper, CQRS, DDD tactical patterns, GraphQL.
This is a store with 90 products and 3 admins. Justify every dependency.

---

## 4. Architecture

Logical separation is maintained even though deployment is simple:

```
Next.js (SSR/SSG)  →  ASP.NET Core API  →  EF Core  →  PostgreSQL
```

The frontend never touches the database. The API is the only writer.

**Layer responsibilities**

- **Endpoint** — HTTP concerns only: route, bind, validate input shape, map result
  to a status code. No business rules, no `DbContext`.
- **Service** — business rules and orchestration. Knows nothing about HTTP.
- **DbContext / EF Core** — persistence. No business rules.

The service layer is introduced when EF Core lands, not before. A service wrapping
a hardcoded array is ceremony, not architecture.

---

## 5. Domain model (current)

```
Brand  1 ──── N  Product  1 ──── N  ProductVariant
```

**Product** — id, name, slug, description, brand_id
**ProductVariant** — id, product_id, tone_code?, tone_name?, size?, sell_price,
cost_price, physical_stock, available_on_demand, is_active

Notes that are easy to get wrong:

- **Price lives on the variant, not the product** (ADR-0003). Real example: Milagros
  Perfume Capilar is $8.000 in 30ml and $27.000 in 120ml.
- **Slugs are brand-prefixed** (ADR-0002). `ani-k-polvo-suelto`, not `polvo-suelto` —
  three different brands sell a "Polvo Suelto".
- `tone_code` and `tone_name` are separate columns. Bloomshell ships a tone coded
  `5.5 Desert`; Montoc uses bare numerics like `100`, `940`.
- `size` is free text for now (`300 ml`, `140 g`, `180 unidades`). Not normalized
  until there is a reason.
- `available_stock` is **never stored**. It is derived (ADR-0007).

---

## 6. Conventions

- **API routes:** `/api/products`, `/api/products/{slug}`. Plural nouns, lowercase.
- **Endpoint signatures:** use typed results —
  `Results<Ok<ProductDto>, NotFound>`. Never bare `IResult`; it produces an empty
  OpenAPI spec.
- **Endpoint grouping:** one static class per resource with a
  `MapProductEndpoints(this IEndpointRouteBuilder app)` extension. `Program.cs`
  stays thin.
- **Slug lookup:** lowercase the incoming slug in C#, then query
  `WHERE slug = @p`. Never `WHERE LOWER(slug) = @p` — it defeats the index.
- **Culture:** always `ToLowerInvariant()`, never `ToLower()`.
- **Null checks:** `is null` / `is not null`, not `== null`.
- **Money:** `decimal` in C#, `numeric` in Postgres. Never `float`/`double`.
- **Migrations:** one per logical change, descriptive name. Never edit an applied
  migration; add a new one.
- **Nullable reference types:** enabled. Do not suppress warnings to make code compile.

---

## 7. Where things live

```
Brava.slnx               solution file — build/run this, not a single .csproj
Brava.Domain/             entities only, zero package references
Brava.Application/        IBravaDbContext + (future) services, DTOs
Brava.Infrastructure/     BravaDbContext, migrations, CSV seeder
Brava.Api/                Minimal API endpoints, Program.cs, DTOs
/data                     seed CSVs (products, variants)
/docs/adr                 architecture decision records
```

4-project Clean Architecture layering (Domain ← Application ← Infrastructure ←
Api), added 2026-08-16. Minimal APIs and plain Services kept — no Controllers,
no CQRS, no per-entity repositories. `IBravaDbContext` is the one seam between
Application and Infrastructure. This isn't yet written up as an ADR — worth one
if it should stick.

---

## 8. Current status

**Shipped:** PostgreSQL + EF Core, seeded from `/data/*.csv`. `GET /api/products`,
`GET /api/brands`, and `POST /api/products` against the real database.

**In progress:** admin login, so `POST /api/products` (currently unauthenticated —
see ADR-0008) can move behind auth before wider exposure.

**Next, in order:** admin login → more admin write endpoints (variants, images) →
product listing page in Next.js → product detail page → WhatsApp deep link → v1 launch.

**Explicitly out of scope for v1** (ADR-0005, amended by ADR-0008): cart, checkout,
online payment, user accounts, stock reservations, coupons, newsletter. Admin
write endpoints are no longer fully deferred — see ADR-0008 — but the admin
**panel UI** is still ahead of v1.

Two blanks only the business owners can fill: `stock` on every variant and
`cost_price` (cost) on every product. Both are empty in the seed CSVs by design.

---

## 9. Open questions

- Product listing shows `Desde $X` when a product's variants have different prices,
  and a plain `$X` when they don't. The `MIN`/`MAX` aggregate must be computed over
  **active variants only**.
- 11 rows in the seed CSVs are flagged `needs_review` (missing or truncated prices
  in the source PDF). They must not be published with a null price.
