'use client'

import { useRouter } from 'next/navigation'
import { useCallback, useEffect, useState } from 'react'

import { InvoiceViewer } from '@/components/invoice-viewer'
import { NavBar } from '@/components/nav-bar'
import { RiskBadge } from '@/components/risk-badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { api, ApiError } from '@/lib/api-client'
import { clearAuth, getAuth } from '@/lib/auth'
import { formatInr, type ReviewQueueItem } from '@/lib/types'
import { decisionSchema } from '@/lib/zod-schemas'

const REASON_CODES = ['DUPLICATE_INVOICE', 'OUT_OF_COVERAGE', 'PRE_EXISTING', 'INFLATED_BILL', 'VERIFIED_OK']

export default function AdjudicatePage() {
  const router = useRouter()
  const [queue, setQueue] = useState<ReviewQueueItem[]>([])
  const [selected, setSelected] = useState<ReviewQueueItem | null>(null)
  const [decision, setDecision] = useState<'APPROVED' | 'REJECTED'>('APPROVED')
  const [approvedAmount, setApprovedAmount] = useState('')
  const [reasonCode, setReasonCode] = useState(REASON_CODES[0])
  const [remarks, setRemarks] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const loadQueue = useCallback(async () => {
    try {
      const items = await api.reviewQueue('MANUAL_REVIEW')
      setQueue(items)
      setSelected((current) => items.find((item) => item.id === current?.id) ?? items[0] ?? null)
      setError(null)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not reach the API. Is the backend running?')
    }
  }, [])

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
    void loadQueue()
  }, [router, loadQueue])

  function selectClaim(item: ReviewQueueItem) {
    setSelected(item)
    setApprovedAmount(String(item.claimedAmount))
    setRemarks('')
    setNotice(null)
  }

  async function handleDecide(event: React.FormEvent) {
    event.preventDefault()
    setError(null)
    setNotice(null)
    if (!selected) return

    const parsed = decisionSchema.safeParse({
      claimId: selected.id,
      decision,
      approvedAmount: decision === 'APPROVED' ? Number(approvedAmount) : undefined,
      reasonCode,
      remarks: remarks || undefined,
    })
    if (!parsed.success) {
      setError(parsed.error.issues[0]?.message ?? 'Invalid decision data')
      return
    }

    setBusy(true)
    try {
      const result = await api.decide({
        claimId: parsed.data.claimId,
        decision: parsed.data.decision,
        approvedAmount: parsed.data.approvedAmount,
        reasonCode: parsed.data.reasonCode,
        remarks: parsed.data.remarks,
      })
      setNotice(`${result.claimNumber} ${parsed.data.decision.toLowerCase()} — audit entry recorded and settlement event published.`)
      await loadQueue()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Decision failed.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="min-h-screen">
      <NavBar />
      <main className="mx-auto max-w-6xl px-4 py-8">
        <h1 className="text-2xl font-semibold tracking-tight">Manual review queue</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          The 1-Minute Co-Pilot: highest-risk claims first, AI extraction side-by-side with policy limits.
        </p>

        {error && (
          <p role="alert" className="mt-4 rounded-md bg-red-500/10 px-3 py-2 text-sm text-red-600 dark:text-red-400">
            {error}
          </p>
        )}
        {notice && (
          <p role="status" className="mt-4 rounded-md bg-emerald-500/10 px-3 py-2 text-sm text-emerald-600 dark:text-emerald-400">
            {notice}
          </p>
        )}

        <div className="mt-6 grid gap-6 lg:grid-cols-[320px_1fr]">
          <Card className="h-fit">
            <CardHeader>
              <CardTitle>Queue ({queue.length})</CardTitle>
              <CardDescription>Ranked by AI risk score.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-2">
              {queue.length === 0 ? (
                <p className="text-sm text-muted-foreground">Queue is clear. Nice.</p>
              ) : (
                queue.map((item) => (
                  <button
                    key={item.id}
                    type="button"
                    onClick={() => selectClaim(item)}
                    className={`w-full rounded-lg border p-3 text-left transition-colors ${
                      selected?.id === item.id
                        ? 'border-orange-500 bg-orange-500/5'
                        : 'border-border hover:bg-accent'
                    }`}
                  >
                    <div className="flex items-center justify-between gap-2">
                      <span className="text-sm font-medium">{item.claimNumber}</span>
                      <RiskBadge band={item.aiRiskBand} score={item.aiRiskScore} />
                    </div>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {item.holderName} · {item.policyNumber} · {formatInr(item.claimedAmount)}
                    </p>
                  </button>
                ))
              )}
              <Button variant="outline" size="sm" className="w-full" onClick={() => void loadQueue()}>
                Refresh queue
              </Button>
            </CardContent>
          </Card>

          {selected ? (
            <div className="space-y-6">
              <Card>
                <CardHeader>
                  <CardTitle>{selected.claimNumber} · {selected.holderName}</CardTitle>
                  <CardDescription>
                    {selected.policyNumber} ({selected.policyType}) · Claimed{' '}
                    <span className="font-mono">{formatInr(selected.claimedAmount)}</span> · Remaining balance{' '}
                    <span className="font-mono">{formatInr(selected.remainingBalance)}</span>
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <InvoiceViewer
                    aiReasoning={selected.aiReasoning}
                    claimedAmount={selected.claimedAmount}
                    remainingBalance={selected.remainingBalance}
                    documentUrl={selected.documentUrl}
                  />
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle>Decision</CardTitle>
                  <CardDescription>
                    Reason codes are mandatory and written to the immutable audit trail.
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <form onSubmit={handleDecide} className="space-y-4" noValidate>
                    <div className="flex gap-2">
                      {(['APPROVED', 'REJECTED'] as const).map((option) => (
                        <button
                          key={option}
                          type="button"
                          onClick={() => setDecision(option)}
                          className={`rounded-md border px-4 py-2 text-sm font-medium transition-colors ${
                            decision === option
                              ? option === 'APPROVED'
                                ? 'border-emerald-500 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400'
                                : 'border-red-500 bg-red-500/10 text-red-600 dark:text-red-400'
                              : 'border-border text-muted-foreground hover:bg-accent'
                          }`}
                          aria-pressed={decision === option}
                        >
                          {option === 'APPROVED' ? 'Approve' : 'Reject'}
                        </button>
                      ))}
                    </div>

                    {decision === 'APPROVED' && (
                      <div className="space-y-1.5">
                        <Label htmlFor="approvedAmount">Approved amount (₹)</Label>
                        <Input
                          id="approvedAmount"
                          type="number"
                          min={1}
                          step="0.01"
                          value={approvedAmount}
                          onChange={(e) => setApprovedAmount(e.target.value)}
                          required
                        />
                      </div>
                    )}

                    <div className="space-y-1.5">
                      <Label htmlFor="reasonCode">Reason code</Label>
                      <select
                        id="reasonCode"
                        value={reasonCode}
                        onChange={(e) => setReasonCode(e.target.value)}
                        className="h-9 w-full rounded-md border border-input bg-transparent px-3 text-sm"
                      >
                        {REASON_CODES.map((code) => (
                          <option key={code} value={code}>
                            {code}
                          </option>
                        ))}
                      </select>
                    </div>

                    <div className="space-y-1.5">
                      <Label htmlFor="remarks">Remarks (optional)</Label>
                      <Textarea
                        id="remarks"
                        value={remarks}
                        onChange={(e) => setRemarks(e.target.value)}
                        placeholder="Context for the audit trail…"
                      />
                    </div>

                    <Button
                      type="submit"
                      className={decision === 'APPROVED' ? 'bg-emerald-600 hover:bg-emerald-700' : 'bg-red-600 hover:bg-red-700'}
                      disabled={busy}
                    >
                      {busy ? 'Recording…' : `Record ${decision === 'APPROVED' ? 'approval' : 'rejection'}`}
                    </Button>
                  </form>
                </CardContent>
              </Card>
            </div>
          ) : (
            <Card>
              <CardContent className="pt-6 text-sm text-muted-foreground">
                Select a claim from the queue to open the Co-Pilot view.
              </CardContent>
            </Card>
          )}
        </div>
      </main>
    </div>
  )
}
