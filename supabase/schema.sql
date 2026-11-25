-- ============================================================================
-- ClaimGuard AI — Supabase PostgreSQL schema
-- Run this whole file in the Supabase SQL Editor (New query), then press Run.
-- ============================================================================

-- ---------------------------------------------------------------- extensions
create extension if not exists "pgcrypto";

-- ------------------------------------------------------------------ app_users
create table if not exists app_users (
    id            uuid primary key default gen_random_uuid(),
    email         varchar(255) not null unique,
    full_name     varchar(150) not null,
    role          varchar(30)  not null check (role in ('POLICYHOLDER', 'PARTNER', 'ADJUDICATOR', 'ADMIN')),
    partner_gstin varchar(20),
    created_at    timestamptz  not null default now()
);

-- Custom JWT auth: the .NET API verifies PBKDF2 password hashes.
alter table app_users add column if not exists password_hash text;

-- ------------------------------------------------------------------- policies
create table if not exists policies (
    id                uuid primary key default gen_random_uuid(),
    policy_number     varchar(50)  not null unique,
    holder_id         uuid         not null references app_users(id),
    policy_type       varchar(20)  not null check (policy_type in ('HEALTH', 'MOTOR')),
    sum_insured       numeric(12,2) not null,
    remaining_balance numeric(12,2) not null,
    status            varchar(20)  not null default 'ACTIVE' check (status in ('ACTIVE', 'EXPIRED', 'SUSPENDED')),
    valid_from        date         not null,
    valid_to          date         not null,
    created_at        timestamptz  not null default now()
);

-- --------------------------------------------------------------------- claims
create table if not exists claims (
    id              uuid primary key default gen_random_uuid(),
    claim_number    varchar(40)  not null unique,
    policy_id       uuid         not null references policies(id),
    submitted_by    uuid         not null references app_users(id),
    idempotency_key varchar(100) not null unique,
    incident_date   date         not null,
    claimed_amount  numeric(12,2) not null,
    approved_amount numeric(12,2),
    document_url    text         not null,
    status          varchar(30)  not null default 'SUBMITTED'
                    check (status in ('SUBMITTED', 'AI_SCORING', 'AUTO_APPROVED', 'MANUAL_REVIEW', 'APPROVED', 'REJECTED')),
    ai_risk_score   int          check (ai_risk_score between 0 and 100),
    ai_risk_band    varchar(15)  check (ai_risk_band in ('LOW', 'MEDIUM', 'HIGH')),
    ai_reasoning    jsonb,
    created_at      timestamptz  not null default now(),
    updated_at      timestamptz  not null default now()
);

create index if not exists idx_claims_status_risk on claims (status, ai_risk_band);
create index if not exists idx_claims_policy_id   on claims (policy_id);

-- ------------------------------------------------------------ claim_audit_logs
create table if not exists claim_audit_logs (
    id              uuid primary key default gen_random_uuid(),
    claim_id        uuid        not null references claims(id) on delete cascade,
    actor_type      varchar(20) not null check (actor_type in ('SYSTEM_AI', 'ADJUDICATOR', 'PARTNER', 'POLICYHOLDER')),
    actor_id        uuid        references app_users(id),
    previous_status varchar(30),
    new_status      varchar(30) not null,
    remarks         text        not null,
    kafka_offset    varchar(100),
    created_at      timestamptz not null default now()
);

create index if not exists idx_audit_claim_id on claim_audit_logs (claim_id);

-- ------------------------------------------------------------------ updated_at
create or replace function set_updated_at() returns trigger as $$
begin
    new.updated_at = now();
    return new;
end;
$$ language plpgsql;

drop trigger if exists trg_claims_updated_at on claims;
create trigger trg_claims_updated_at before update on claims
    for each row execute function set_updated_at();

-- ------------------------------------------------- private documents bucket
-- The .NET API uploads with the service-role key; browsers only get signed URLs.
insert into storage.buckets (id, name, public)
values ('claim-documents', 'claim-documents', false)
on conflict (id) do nothing;

-- ------------------------------------------------------------- seed policies
-- 1. Register a POLICYHOLDER through the app (POST /api/auth/register).
-- 2. Copy the returned userId into the statement below and run it once.
--
-- insert into policies (policy_number, holder_id, policy_type, sum_insured, remaining_balance, valid_from, valid_to)
-- values
--     ('POL-2024-HL-000001', '<paste-userId-here>', 'HEALTH', 500000.00, 500000.00, current_date - 180, current_date + 185),
--     ('POL-2024-MT-000002', '<paste-userId-here>', 'MOTOR',  200000.00, 200000.00, current_date - 90,  current_date + 275);
--
-- ADJUDICATOR / ADMIN accounts: register through the API with the bootstrap
-- secret (Seed:BootstrapSecret) — they cannot be self-registered otherwise.
