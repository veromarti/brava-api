# ADR-0007 — `available_stock` is derived, never stored

**Status:** Proposed — not implemented in v1 · **Date:** 2026-08-16

## Context

BRAVA holds physical inventory, sells some items on demand without stock, and will
eventually need to hold units temporarily while a customer completes an order so two
people cannot buy the same unit.

Storing `available_stock` as a column makes it a second source of truth that must be
kept in sync with every reservation, expiry, cancellation, and restock. Every one of
those paths is an opportunity to drift, and drift in inventory is invisible until a
customer is told an item is available that is not.

## Decision

Store `physical_stock` on the variant and reservations in their own table.
`available_stock` is computed:

```
available_stock = physical_stock − SUM(active reservations)
```

`available_on_demand` is a separate boolean. When `physical_stock = 0` and
`available_on_demand` is true, the product page shows "Disponible bajo pedido.
Entrega estimada: 3 días hábiles." When both are false, it shows "Agotado" with an
option to ask about availability over WhatsApp.

## Consequences

- One source of truth. Reconciling stock means counting reservations, not trusting a
  cached number.
- Reads cost a join and an aggregate. At 191 variants this is free; if it ever stops
  being free, a materialized view or cached column is the fix — but only once
  measurement shows it is needed.
- Reservations need an expiry and a job that releases stale ones, or stock leaks.
- The decrement path must run inside a transaction with appropriate locking, or two
  concurrent checkouts can both read the same availability and both succeed.
  This is the single hardest correctness problem in the system.
- **Not implemented in v1** (ADR-0005). v1 displays stock without reserving it.
  The schema is designed so reservations can be added without reshaping the model.
