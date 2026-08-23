# ADR-0008 — Write endpoints start before the admin panel

**Status:** Accepted · **Date:** 2026-08-16 · **Amends:** ADR-0005

## Context

ADR-0005 deferred all write access to "direct database access or a seeding
script until the admin panel exists." That assumption breaks down as soon as
the catalog isn't static: BRAVA's partners need to keep adding new products on
an ongoing basis, and doing that by hand through SQL doesn't scale past the
initial 90-product seed — every new product would mean pulling in the one
developer on the team to run a script or write an INSERT by hand.

The admin panel itself is still ahead of us. Rather than wait for the full UI,
the write API starts now: `POST /api/products` is the first endpoint, and
admin login is the next piece of work, so the eventual admin panel talks to an
API that already exists instead of the two being built together.

## Decision

- `POST /api/products` exists; `GET`/`PUT`/`DELETE` and variant endpoints
  don't yet.
- A product can be created with zero variants. ADR-0003's "no product without
  variants in the public catalog" rule stays enforced at the listing-query
  level (products with zero active/priced variants are excluded from
  `GET /api/products`), not at creation time. This is the intended split:
  partners create the product first, variants get added after — a brief
  zero-variant state is expected, not a bug.
- Slug is server-generated via the same `SlugGenerator` the CSV seeder uses
  (ADR-0002), with collisions auto-suffixed (`-2`, `-3`, ...) rather than
  rejected with `409`.
- `BrandId`/`CategoryId` are validated to exist before insert (`404` if not),
  rather than relying on the FK constraint to fail.
- **No authentication on this endpoint yet.** That's a known, temporary gap,
  not an oversight — admin login is the immediate next step, and this
  endpoint should be treated as not safe for wider exposure until it's gated
  behind it.

## Consequences

- CLAUDE.md's roadmap (section 8) needs updating: "admin panel" moves from
  fully out-of-scope-for-v1 to partially started (the write API), with admin
  login as the next concrete step.
- Until admin login lands, `POST /api/products` is reachable by anyone who can
  reach the API. Acceptable short-term since the deployed environment isn't
  widely shared yet, but this must not go out to a public Railway URL without
  auth in front of it.
- Every future write endpoint (variants, images, brands, categories) inherits
  this same "build now, not after a full admin UI" approach unless a reason
  shows up to treat one differently.
