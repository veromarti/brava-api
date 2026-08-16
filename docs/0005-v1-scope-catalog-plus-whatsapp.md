# ADR-0005 — v1 is a read-only catalog with WhatsApp handoff

**Status:** Accepted · **Date:** 2026-08-16

## Context

Business partners are waiting for a first version. The full plan — cart, checkout,
stock reservations, accounts, admin panel, reports — is months of work for one
junior developer, and every week of delay is another week of sending a 40 MB PDF
over WhatsApp.

BRAVA's existing purchase flow is already documented on page 3 of the catalog:
the customer picks products, messages WhatsApp with the reference and quantity,
BRAVA confirms availability and total, the customer transfers or pays on delivery.

Every step after the first already works and requires no software.

## Decision

v1 ships a **public, read-only catalog** with:

- brand and category browsing, product detail pages on slugs
- a per-product **"Pedir por WhatsApp"** button that opens `wa.me` with a
  pre-filled message containing product name, tone/size, and quantity
- SEO fundamentals: server-rendered pages, slug URLs, metadata, Open Graph images

**Out of scope for v1:** cart, checkout, online payment, user accounts, order
history, stock reservations, admin panel, coupons, combos, newsletter, loyalty.

Stock is displayed but not reserved. Availability is still confirmed by a human over
WhatsApp, exactly as today.

## Consequences

- v1 replaces step 1 of the existing process and changes nothing downstream, so it
  can launch without retraining the three admins or changing how money is collected.
- No cart means no reservation logic, no concurrency handling, and no transaction
  design in v1 — the three hardest parts of the system are deferred until there is
  real order volume to design against.
- Product and stock data is edited by direct database access or a seeding script
  until the admin panel exists. Acceptable for 3 admins and 90 products; it will
  stop being acceptable quickly, so the admin panel is the first thing after v1.
- Displayed stock can go stale between page load and WhatsApp confirmation. This is
  the same failure mode the PDF already has, so it is not a regression.
- Risk: "just add a cart" pressure will arrive the week after launch. The reason to
  wait is that reservation logic built without observed order volume is guesswork.
