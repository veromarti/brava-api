# ADR-0003 — Price lives on ProductVariant, not Product

**Status:** Accepted · **Date:** 2026-08-16 · **Supersedes:** the initial model
sketch, which placed `unitary_price` and `sell_price` on `Product`.

## Context

The original model assumed one price per product, with variants differing only in
tone and size. Parsing the real catalog disproved this:

- Milagros Perfume Capilar — $8.000 (30 ml) / $27.000 (120 ml)
- Click Hair Miel Capilar — $25.000 (20 ml) / $55.000 (50 ml)
- Montoc Fijador Dixy Fix — different price per size

Three products out of ninety break the assumption today. Size-based pricing is
normal in cosmetics, so the count will grow, and the cost of moving price after
checkout logic exists is far higher than moving it now.

## Decision

`sell_price` and `unitary_price` move to `ProductVariant`. `Product` carries no
price at all.

Listing pages compute `MIN(sell_price)` and `MAX(sell_price)` over the product's
**active** variants. The frontend renders a single price when min equals max, and
`Desde $X` when they differ.

## Consequences

- Every product must have at least one variant, including products with no tone and
  no size. Those get a single variant with null tone and null size. There is no
  "product without variants" path in the code.
- A product with zero active variants must be excluded from listings entirely,
  otherwise `MIN` is null and the page renders a broken price.
- The `MIN`/`MAX` aggregate must filter on `is_active`. Advertising `Desde $8.000`
  for a size that is discontinued or out of stock produces a WhatsApp conversation
  where the quoted price is three times the advertised one.
- 84 of 90 products have a single price, so most cards will show a plain price.
  `Desde` must not appear when min equals max, or the catalog reads as a
  bait-and-switch.
- Price changes are now per-variant edits in the admin panel. Slightly more work for
  the 3 admins, and correct.
