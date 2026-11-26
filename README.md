# ClaimGuard AI — Intelligent Claims Adjudication Platform

Event-driven insurance claims platform for **IL-Pulse (ICICI Lombard × Smart India Hackathon 2024)**:
AI fraud triage, instant cashless pre-authorization, duplicate-proof submissions, an immutable
regulatory audit trail, and a 1-Minute Co-Pilot for human adjudicators.

**Stack:** .NET 8 (Clean Architecture) · Supabase PostgreSQL · Upstash Redis · Upstash Kafka ·
CloudAMQP (RabbitMQ) · Google Gemini · Next.js 16 · Tailwind · Prometheus · Kubernetes · GitHub Actions

---

## File structure

```
.
├── backend/
│   ├── ClaimGuard.sln
│   ├── src/
│   │   ├── ClaimGuard.Domain/            # Entities, Enums, Events (zero dependencies)
│   │   ├── ClaimGuard.Application/       # Interfaces, DTOs, Services (pure logic)
│   │   ├── ClaimGuard.Infrastructure/    # EF Core, Redis, Kafka, RabbitMQ, Gemini, JWT, Storage
│   │   └── ClaimGuard.Api/               # Controllers, Middleware, Program.cs, Dockerfile
│   └── tests/ClaimGuard.UnitTests/       # xUnit + Moq
├── app/                                  # Next.js frontend (repo root = frontend/)
│   ├── (auth)/login/page.tsx             # Login + self-signup
│   ├── portal/page.tsx                   # Policyholder: submit & track claims
│   ├── partner/page.tsx                  # Cashless pre-authorization desk
│   ├── ops/adjudicate/page.tsx           # 1-Minute Co-Pilot review queue
│   └── ops/audit/page.tsx                # Immutable audit trail + system status
├── components/                           # ClaimTimeline, RiskBadge, InvoiceViewer, ui/*
├── lib/                                  # api-client, supabase-client, zod-schemas, auth, types
├── __tests__/                            # Jest tests (zod schemas + components)
├── supabase/schema.sql                   # Full DDL — run once in the Supabase SQL Editor
├── k8s/                                  # namespace, secrets.example, deployments, prometheus
├── .github/workflows/                    # ci-quality-security.yml, cd-k8s-deploy.yml
├── docker-compose.lite.yml               # Local: API + Web only (cloud services stay remote)
├── Dockerfile.web                        # Next.js production image
├── sonar-project.properties
└── .env.example
```

---

## Setup guide

### A. Provision the free-tier cloud services

| Service | Provider | What to create |
|---|---|---|
| PostgreSQL | Supabase (free) | One project |
| Redis | Upstash (free) | One Regional database (RESP endpoint) |
| Kafka | Upstash (free) | One cluster + 3 topics: `claim.submitted`, `claim.scored`, `claim.settled` (1 partition each) |
| RabbitMQ | CloudAMQP (Lemur, free) | One instance |
| AI | Google AI Studio (free) | One Gemini API key |

### B. Create the database schema

Supabase Dashboard → **SQL Editor** → New query → paste **`supabase/schema.sql`** → **Run**.
This creates `app_users`, `policies`, `claims`, `claim_audit_logs`, the private
`claim-documents` storage bucket, and the `updated_at` trigger.

### C. Configure environment

```bash
cp .env.example .env          # for docker compose
cp .env.example .env.local    # for Next.js dev (only the NEXT_PUBLIC_* keys matter here)
```

Fill in every value from step A. Generate the JWT secret with `openssl rand -hex 32`.

### D. Run the backend

```bash
cd backend
dotnet restore
dotnet run --project src/ClaimGuard.Api     # https://localhost:5000 (Swagger at /swagger)
```

Or from the repo root, one command that also maps `.env` keys to the config
names ASP.NET Core expects:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run-api-local.ps1
```

All cloud credentials are read from `.env` — export them or paste them into
`src/ClaimGuard.Api/appsettings.Development.json` (git-ignored). Missing Kafka/RabbitMQ/Redis
config degrades gracefully: the consumer idles and the deterministic scorer is used instead of Gemini.

### E. Run the frontend

```bash
pnpm install
pnpm dev        # http://localhost:3000
```

Or one command that also generates `.env.local` from `.env`:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run-web-local.ps1
```

### F. First accounts & demo data

1. Register a **Policyholder** at `/login` → Create an account.
2. Register an **Adjudicator** via the API with the bootstrap secret:
   ```bash
   curl -X POST http://localhost:5000/api/auth/register \
     -H "Content-Type: application/json" \
     -d '{"email":"ops@claimguard.in","fullName":"Ops Admin","password":"Str0ngPass!","role":"ADJUDICATOR","bootstrapSecret":"<SEED_BOOTSTRAP_SECRET>"}'
   ```
3. Seed policies: uncomment the block at the bottom of `supabase/schema.sql`,
   paste the policyholder's `userId`, and run it in the SQL Editor.

### G. Verify the end-to-end flow

1. **Policyholder** (`/portal`): submit a claim with a document → status `SUBMITTED`.
2. Within ~60s the Kafka consumer scores it → `AUTO_APPROVED` (low risk, ≤ ₹10,000) or `MANUAL_REVIEW`.
3. **Adjudicator** (`/ops/adjudicate`): open the Co-Pilot view, approve/reject with a reason code.
4. **Audit** (`/ops/audit`): every transition is recorded with its Kafka offset.
5. **Idempotency**: replay the same `X-Idempotency-Key` → HTTP 409, no double payout.
6. **Metrics**: `curl http://localhost:5000/metrics` (Prometheus format).

### Tests & quality gates

```bash
cd backend && dotnet test                 # xUnit: fraud rules + idempotency + authorization
pnpm test                                 # Jest: zod schemas + UI components
```

CI (`.github/workflows/ci-quality-security.yml`) runs both suites plus CodeQL (C# + JavaScript).
CD (`.github/workflows/cd-k8s-deploy.yml`) builds both Docker images to GHCR and rolls them out
to Kubernetes with readiness/liveness probes.

### Local Docker (lite) — optional

The app runs fully without Docker (sections D–E). Containerization is only
needed if you want to reproduce the container deployment locally:

```bash
docker compose -f docker-compose.lite.yml up --build
```

Only the stateless services are containerized; Postgres/Redis/Kafka/RabbitMQ remain on their
cloud tiers, identical to production.

### Kubernetes — optional

The CD workflow (`cd-k8s-deploy.yml`) is currently disabled (manual trigger
only). To use it again, uncomment its push/tags triggers and:

```bash
cp k8s/secrets.example.yaml k8s/secrets.yaml   # fill in, never commit
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/
```

---

## Architecture notes

- **Clean Architecture**: Domain has zero dependencies; Application defines interfaces;
  Infrastructure implements them; Api is a thin composition root.
- **Event flow**: `POST /api/claims` → `claim.submitted` (Kafka) → triage consumer →
  `claim.scored` → adjudicator decision → `claim.settled` → RabbitMQ notifications (DLQ on failure).
- **AI with a safety net**: Gemini returns structured JSON (score/band/anomalies/line items);
  any failure falls back to the deterministic rule engine, so triage never stalls.
- **Security**: JWT with role claims, PBKDF2 password hashing, Redis rate limiting (60 req/min/IP),
  mandatory idempotency keys, private document bucket with server-side service-role key only,
  and 1-hour signed URLs for adjudicator viewing.
