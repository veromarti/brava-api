# ADR-0004 — Minimal APIs, not MVC Controllers

**Status:** Accepted · **Date:** 2026-08-16

## Context

The .NET template scaffolded minimal APIs. The original learning roadmap planned a
migration to Controllers as an explicit step, on the assumption that Controllers are
the "proper" architecture.

The concern with minimal APIs is that `Program.cs` becomes a dumping ground of
inline lambdas. The concern with Controllers is that they bring attribute routing,
filters, model binding conventions, and action results that a nine-brand catalog
with three admins does not need.

## Decision

Stay on minimal APIs. Prevent `Program.cs` sprawl with one static class per
resource exposing an extension method:

```csharp
app.MapProductEndpoints();
```

Endpoints use typed results — `Results<Ok<ProductDto>, NotFound>` — rather than bare
`IResult`. This is not cosmetic: bare `IResult` produces an OpenAPI document with no
response schema and no documented 404, which makes the generated spec useless to the
frontend.

## Consequences

- `Program.cs` stays short and readable, which is what the Controller migration was
  actually trying to buy.
- Minimal APIs are where the .NET platform is investing, so this is the more current
  skill to demonstrate.
- Cross-cutting concerns (auth, validation, logging) arrive as endpoint filters
  rather than MVC filters. Different mechanism, comparable ergonomics.
- If a future feature genuinely needs MVC — server-rendered Razor views, complex
  model binding — Controllers can be added alongside minimal APIs in the same app.
  This decision is reversible and cheap to reverse.
- **Noted honestly:** if Controllers are wanted for CV coverage, that is a career
  reason, not an architectural one. It should be argued as such, not disguised as a
  technical requirement.
