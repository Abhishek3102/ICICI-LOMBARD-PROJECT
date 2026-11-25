'use client'

import { useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'

import { NavBar } from '@/components/nav-bar'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { api, ApiError, API_BASE } from '@/lib/api-client'
import { clearAuth, getAuth } from '@/lib/auth'
import type { AuditLogEntry } from '@/lib/types'

const actorVariant: Record<string, 'default' | 'secondary' | 'warning' | 'success'> = {
  SYSTEM_AI: 'secondary',
  ADJUDICATOR: 'default',
  PARTNER: 'warning',
  POLICYHOLDER: 'success',
}

export default function AuditPage() {
  const router = useRouter()
  const [logs, setLogs] = useState<AuditLogEntry[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    const auth = getAuth()
    if (!auth) {
      router.push('/login')
      return
    }
    if (auth.role !== 'ADJUDICATOR' && auth.role !== 'ADMIN') {
      router.push(auth.role === 'PARTNER' ? '/partner' : '/portal')
      return
    }

    api
      .auditLogs(150)
      .then((data) => {
        setLogs(data)
        setError(null)
      })
      .catch((err: unknown) =>
        setError(err instanceof ApiError ? err.message : 'Could not reach the API. Is the backend running?'),
      )
  }, [router])

  return (
    <div className="min-h-screen">
      <NavBar />
      <main className="mx-auto max-w-6xl px-4 py-8">
        <h1 className="text-2xl font-semibold tracking-tight">Audit trail</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Immutable record of every AI and human decision, linked to its Kafka offset for replay.
        </p>

        {error && (
          <p role="alert" className="mt-4 rounded-md bg-red-500/10 px-3 py-2 text-sm text-red-600 dark:text-red-400">
            {error}
          </p>
        )}

        <div className="mt-6 grid gap-4 sm:grid-cols-3">
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Event stream</CardTitle>
              <CardDescription>
                Topics: claim.submitted → claim.scored → claim.settled (Kafka, 1 partition each).
              </CardDescription>
            </CardHeader>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Dead-letter queue</CardTitle>
              <CardDescription>
                Failed notifications land in notifications.dlq (RabbitMQ DLX) for inspection and retry.
              </CardDescription>
            </CardHeader>
          </Card>
          <Card>
            <CardHeader>
              <CardTitle className="text-sm">Metrics</CardTitle>
              <CardDescription>
                Prometheus scrape target:{' '}
                <code className="rounded bg-muted px-1 py-0.5 text-xs">{API_BASE}/metrics</code>
              </CardDescription>
            </CardHeader>
          </Card>
        </div>

        <Card className="mt-6">
          <CardHeader>
            <CardTitle>Recent entries ({logs.length})</CardTitle>
          </CardHeader>
          <CardContent>
            {logs.length === 0 ? (
              <p className="text-sm text-muted-foreground">No audit entries yet.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-sm">
                  <thead>
                    <tr className="border-b border-border text-xs text-muted-foreground">
                      <th className="py-2 pr-4 font-medium">When</th>
                      <th className="py-2 pr-4 font-medium">Claim</th>
                      <th className="py-2 pr-4 font-medium">Actor</th>
                      <th className="py-2 pr-4 font-medium">Transition</th>
                      <th className="py-2 pr-4 font-medium">Remarks</th>
                      <th className="py-2 font-medium">Kafka offset</th>
                    </tr>
                  </thead>
                  <tbody>
                    {logs.map((log) => (
                      <tr key={log.id} className="border-b border-border/60 align-top">
                        <td className="py-2 pr-4 whitespace-nowrap text-xs text-muted-foreground">
                          {new Date(log.createdAt).toLocaleString('en-IN')}
                        </td>
                        <td className="py-2 pr-4 font-mono text-xs">{log.claimNumber ?? '—'}</td>
                        <td className="py-2 pr-4">
                          <Badge variant={actorVariant[log.actorType] ?? 'secondary'}>
                            {log.actorType}
                          </Badge>
                          {log.actorName && (
                            <span className="ml-2 text-xs text-muted-foreground">{log.actorName}</span>
                          )}
                        </td>
                        <td className="py-2 pr-4 text-xs">
                          {log.previousStatus ? `${log.previousStatus} → ` : ''}
                          <span className="font-medium">{log.newStatus}</span>
                        </td>
                        <td className="max-w-md py-2 pr-4 text-xs text-muted-foreground">{log.remarks}</td>
                        <td className="py-2 font-mono text-xs text-muted-foreground">
                          {log.kafkaOffset ?? '—'}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </CardContent>
        </Card>

        <button
          type="button"
          className="mt-8 text-xs text-muted-foreground underline-offset-4 hover:underline"
          onClick={() => {
            clearAuth()
            router.push('/login')
          }}
        >
          Sign out
        </button>
      </main>
    </div>
  )
}
