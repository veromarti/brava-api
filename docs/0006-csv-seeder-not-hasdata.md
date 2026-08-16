# ADR-0006 — Seed from CSV at startup, not from EF Core `HasData`

**Status:** Provisional · **Date:** 2026-08-16

## Context

The catalog was extracted from the Canva PDF into `/data/brava_products.csv`
(90 rows) and `/data/brava_variants.csv` (191 rows). Two columns are intentionally
empty and will be filled by the business owners: `stock` on every variant and
`unitary_price` on every product.

Two ways to get this into Postgres:

- **`HasData` in the EF Core model** — data becomes part of migrations, applied
  automatically, versioned with the schema.
- **A seeding routine that reads the CSVs at startup in Development.**

## Decision

Use a CSV seeder, idempotent, keyed on slug, run only in Development and on first
deploy. `HasData` is reserved for genuinely static reference data if any appears.

The deciding factor is `stock`. `HasData` bakes all 191 rows into a migration file,
so every stock correction from the owners becomes a new migration in source control.
Stock is operational data that will eventually be edited through the admin panel; it
does not belong in schema version history.

## Consequences

- Product data can be corrected in a spreadsheet by non-developers and re-seeded,
  which matters while 11 rows are still flagged `needs_review` for missing prices.
- The seeder is code that must be maintained and must be idempotent — re-running it
  must update existing rows by slug, not duplicate them.
- Seed data is not versioned alongside schema, so a fresh environment needs both the
  migrations and the CSVs. Keep `/data` in the repository.
- The seeder must refuse to publish rows with a null `sell_price`. Flagged rows are
  imported as inactive until a price is confirmed.
- **Provisional:** if the CSVs turn out to be edited rarely and the admin panel
  lands sooner than expected, this is worth revisiting. Changing it later is a
  contained piece of work.
