# ADR-0002 — Slugs are brand-prefixed, lowercase, ASCII-folded

**Status:** Accepted · **Date:** 2026-08-16

## Context

Product URLs use slugs rather than IDs, for SEO and shareability:
`/products/ani-k-polvo-suelto`.

Two problems surfaced when the real catalog was parsed:

1. **Collisions.** Three different brands sell a product called "Polvo Suelto".
   "Agua Micelar", "Perfume Capilar", and "Lip Balm" each appear under two brands.
   A unique index on a name-derived slug would fail on seed.
2. **Casing.** BRAVA's distribution is WhatsApp and Instagram. Links get forwarded,
   retyped, and autocapitalized by mobile keyboards. A 404 on
   `/products/Ani-K-Polvo-Suelto` is a lost sale.

Spanish names also carry diacritics — `Pestañina`, `Áloe` — which must not appear
raw in URLs.

## Decision

Slugs are generated **on write** from `brand + name`, lowercased, Unicode-normalized
with combining marks stripped (`ñ` → `n`, `á` → `a`), non-alphanumerics collapsed to
a single hyphen. A unique index enforces the result.

Lookup is case-insensitive: the API lowercases the incoming slug **in C#**, then
queries `WHERE slug = @p`. Never `WHERE LOWER(slug) = @p` — that prevents Postgres
from using the index and turns an O(log n) lookup into a table scan.

The lowercase form is canonical. The Next.js frontend issues a **301 redirect** from
any non-canonical casing to it.

## Consequences

- Two identical-looking URLs never both return 200, so Google indexes one canonical
  page and ranking signal is not split across duplicates.
- Non-canonical requests cost one extra round trip. Irrelevant at this traffic.
- Renaming a product changes its slug and breaks inbound links. When products start
  being renamed, a `slug_history` table with 301 redirects will be needed. Not now.
- The brand prefix makes URLs longer but also more descriptive, which is a mild SEO
  benefit and a real usability one when links are pasted into chat.
- The slugify function must be identical in C# and TypeScript, or the frontend will
  generate links the API cannot resolve. Keep it in one place per side and test it
  against the accented product names in the seed data.
