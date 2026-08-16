# ADR-0001 — Host the MVP on Railway

**Status:** Accepted · **Date:** 2026-08-16

## Context

BRAVA needs three running pieces: an ASP.NET Core API, a Next.js frontend, and a
PostgreSQL database. The team is one junior developer with no production deployment
experience, and the business is pre-revenue with a limited budget. Expected traffic
is unknown but small — the current customer base orders through WhatsApp.

Options considered:

- **Railway** — usage-based, ~$5/month minimum, all three services on one platform.
- **Render** — fixed per-service pricing, roughly $20/month for the same three
  pieces, more predictable.
- **Vercel + Neon + Railway** — the "modern" split. Vercel's Hobby plan forbids
  commercial use, so this starts at $20/month for Vercel alone.
- **Hetzner VPS + Docker Compose** — around €5/month for everything, but the team
  owns backups, TLS, patching, and uptime.

## Decision

Deploy API, web, and Postgres to a single Railway project, US East region.

Usage-based billing means the pre-launch months cost close to nothing, and one
platform means one deployment story instead of three. Expected cost is $5–15/month,
which is less than a single product sale — hosting is not a meaningful cost driver
for this business and should not consume more decision time than this.

The self-hosted VPS option is deferred, not rejected. It is revisited as a
deliberate learning exercise once v1 is live and the Docker/CI/CD phase begins.

## Consequences

- Railway has no permanent free tier; the trial credit runs out and a paid plan is
  required for an always-on service. Budget for this from day one.
- Usage-based billing has no natural ceiling. Set a spend limit.
- Vendor lock-in is low: the app is containerized and Postgres is standard, so
  migrating to a VPS later is a weekend, not a rewrite.
- Region is US East. Latency from Colombia is acceptable; do not split services
  across regions or the internal networking and latency advantages are lost.
- Deployment happens **now**, on the skeleton, not at the end of the roadmap.
  First deploys break on connection strings, CORS, HTTPS, and migrations — all
  problems that are cheap to solve on a two-endpoint app and expensive to solve
  on a finished one.
