import Link from 'next/link'

import { NavBar } from '@/components/nav-bar'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

const capabilities = [
  {
    title: 'AI Triage in under 60 seconds',
    body: 'Every submitted claim streams through Kafka into the Gemini-powered analyzer, which extracts invoice line items, flags anomalies and scores fraud risk 0-100 with a fully explainable audit trail.',
    tag: 'claim.submitted → claim.scored',
  },
  {
    title: 'Duplicate-proof submissions',
    body: 'A Redis idempotency lock plus a database unique constraint means a double-clicked cashless request can never trigger a double payout.',
    tag: 'X-Idempotency-Key',
  },
  {
    title: '1-Minute Co-Pilot for adjudicators',
    body: 'The manual-review queue ranks claims by risk band and shows AI-extracted line items side-by-side with policy limits, so a human decides in minutes, not days.',
    tag: 'MANUAL_REVIEW queue',
  },
  {
    title: 'Immutable regulatory audit',
    body: 'Every status transition — human or AI — appends an audit row linked to its Kafka offset, satisfying IRDAI governance and replay requirements.',
    tag: 'claim_audit_logs',
  },
]

const roles = [
  { href: '/login', title: 'Policyholder', body: 'Track claims live, upload documents, get instant decisions.' },
  { href: '/login', title: 'Partner (Hospital / Garage)', body: 'Cashless pre-authorization desk with instant AI screening.' },
  { href: '/login', title: 'Adjudicator', body: 'Risk-ranked review queue with the 1-Minute Co-Pilot.' },
]

export default function LandingPage() {
  return (
    <div className="min-h-screen">
      <NavBar />

      <main>
        <section className="border-b border-border">
          <div className="mx-auto max-w-6xl px-4 py-16 sm:py-24">
            <Badge variant="warning" className="mb-4">
              IL-Pulse · Smart India Hackathon 2024
            </Badge>
            <h1 className="max-w-3xl text-4xl font-bold tracking-tight sm:text-5xl">
              Insurance claims that decide themselves —{' '}
              <span className="text-orange-600 dark:text-orange-400">with a human still in the loop.</span>
            </h1>
            <p className="mt-4 max-w-2xl text-lg text-muted-foreground">
              ClaimGuard AI is an event-driven claims adjudication platform: AI fraud triage,
              instant cashless pre-authorization, duplicate-proof submissions and an immutable
              audit trail — built on .NET 8, Kafka, Redis, RabbitMQ, Supabase and Next.js.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <Link
                href="/login"
                className="inline-flex h-10 items-center rounded-md bg-orange-600 px-5 text-sm font-medium text-white transition-colors hover:bg-orange-700"
              >
                Open the console
              </Link>
              <Link
                href="/ops/audit"
                className="inline-flex h-10 items-center rounded-md border border-border px-5 text-sm font-medium transition-colors hover:bg-accent"
              >
                See the audit trail
              </Link>
            </div>
          </div>
        </section>

        <section className="mx-auto max-w-6xl px-4 py-16">
          <h2 className="text-2xl font-semibold tracking-tight">How the pipeline works</h2>
          <div className="mt-6 grid gap-4 sm:grid-cols-2">
            {capabilities.map((item) => (
              <Card key={item.title}>
                <CardHeader>
                  <CardTitle>{item.title}</CardTitle>
                  <CardDescription>{item.body}</CardDescription>
                </CardHeader>
                <CardContent>
                  <code className="rounded bg-muted px-2 py-1 text-xs">{item.tag}</code>
                </CardContent>
              </Card>
            ))}
          </div>
        </section>

        <section className="border-t border-border bg-muted/40">
          <div className="mx-auto max-w-6xl px-4 py-16">
            <h2 className="text-2xl font-semibold tracking-tight">Pick your console</h2>
            <div className="mt-6 grid gap-4 sm:grid-cols-3">
              {roles.map((role) => (
                <Link key={role.title} href={role.href} className="group">
                  <Card className="h-full transition-colors group-hover:border-orange-500/60">
                    <CardHeader>
                      <CardTitle>{role.title}</CardTitle>
                      <CardDescription>{role.body}</CardDescription>
                    </CardHeader>
                  </Card>
                </Link>
              ))}
            </div>
          </div>
        </section>
      </main>

      <footer className="border-t border-border py-8">
        <p className="mx-auto max-w-6xl px-4 text-xs text-muted-foreground">
          ClaimGuard AI · Clean Architecture (.NET 8) · Event-driven triage · Prometheus metrics at /metrics
        </p>
      </footer>
    </div>
  )
}
