'use client'

import { useRouter } from 'next/navigation'
import { useCallback, useEffect, useState } from 'react'

import { ClaimTimeline } from '@/components/claim-timeline'
import { NavBar } from '@/components/nav-bar'
import { RiskBadge } from '@/components/risk-badge'
import { StatusBadge } from '@/components/status-badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { api, ApiError } from '@/lib/api-client'
import { clearAuth, getAuth } from '@/lib/auth'
import { formatInr, type ClaimResponse, type PolicyDto } from '@/lib/types'
import { submitClaimSchema } from '@/lib/zod-schemas'

export default function PortalPage() {
  const router = useRouter()
  const [policies, setPolicies] = useState<PolicyDto[]>([])
  const [claims, setClaims] = useState<ClaimResponse[]>([])
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)

  const [policyId, setPolicyId] = useState('')
  const [incidentDate, setIncidentDate] = useState('')
  const [amount, setAmount] = useState('')
  const [file, setFile] = useState<File | null>(null)

  const refreshClaims = useCallback(async () => {
    try {
      setClaims(await api.myClaims())
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
    if (auth.role !== 'POLICYHOLDER') {
      router.push(auth.role === 'PARTNER' ? '/partner' : '/ops/adjudicate')
      return
    }

    async function load() {
      try {
        const [policyList, claimList] = await Promise.all([api.myPolicies(), api.myClaims()])
        setPolicies(policyList)
        setClaims(claimList)
        if (policyList.length > 0) setPolicyId(policyList[0].id)
        setError(null)
      } catch (err) {
        setError(err instanceof ApiError ? err.message : 'Could not reach the API. Is the backend running?')
      } finally {
        setLoading(false)
      }
    }
    void load()
  }, [router])

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault()
    setError(null)
    setNotice(null)

    const parsed = submitClaimSchema.safeParse({
      policyId,
      incidentDate,
      claimedAmount: Number(amount),
      documentUrl: file ? 'pending-upload' : '',
      idempotencyKey: crypto.randomUUID(),
    })
    if (!parsed.success) {
      setError(parsed.error.issues[0]?.message ?? 'Invalid claim data')
      return
    }
    if (!file) {
      setError('Attach the discharge summary or repair estimate (PDF/JPEG/PNG, max 10 MB).')
      return
    }

    setBusy(true)
    try {
      const upload = await api.uploadDocument(file)
      const idempotencyKey = crypto.randomUUID()
      const claim = await api.submitClaim(
        {
          policyId: parsed.data.policyId,
          incidentDate: parsed.data.incidentDate,
          claimedAmount: parsed.data.claimedAmount,
          documentUrl: upload.documentUrl,
          idempotencyKey,
        },
        idempotencyKey,
      )
      setNotice(`Claim ${claim.claimNumber} submitted. AI triage usually completes within a minute.`)
      setFile(null)
      setAmount('')
      await refreshClaims()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Submission failed. Please retry with a fresh request.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="min-h-screen">
      <NavBar />
      <main className="mx-auto max-w-6xl px-4 py-8">
        <h1 className="text-2xl font-semibold tracking-tight">My claims</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Submit a claim and watch the AI triage pipeline progress in real time.
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

        <div className="mt-6 grid gap-6 lg:grid-cols-[380px_1fr]">
          <Card>
            <CardHeader>
              <CardTitle>New claim</CardTitle>
              <CardDescription>Documents upload to a private, signed-URL-only bucket.</CardDescription>
            </CardHeader>
            <CardContent>
              <form onSubmit={handleSubmit} className="space-y-4" noValidate>
                <div className="space-y-1.5">
                  <Label htmlFor="policy">Policy</Label>
                  <select
                    id="policy"
                    value={policyId}
                    onChange={(e) => setPolicyId(e.target.value)}
                    className="h-9 w-full rounded-md border border-input bg-transparent px-3 text-sm"
                    required
                  >
                    {policies.length === 0 && <option value="">No active policies</option>}
                    {policies.map((policy) => (
                      <option key={policy.id} value={policy.id}>
                        {policy.policyNumber} · {policy.policyType} · {formatInr(policy.remainingBalance)} left
                      </option>
                    ))}
                  </select>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="incidentDate">Incident date</Label>
                  <Input
                    id="incidentDate"
                    type="date"
                    value={incidentDate}
                    max={new Date().toISOString().slice(0, 10)}
                    onChange={(e) => setIncidentDate(e.target.value)}
                    required
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="amount">Claimed amount (₹)</Label>
                  <Input
                    id="amount"
                    type="number"
                    min={1}
                    step="0.01"
                    value={amount}
                    onChange={(e) => setAmount(e.target.value)}
                    placeholder="25000"
                    required
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="document">Supporting document</Label>
                  <Input
                    id="document"
                    type="file"
                    accept=".pdf,.jpg,.jpeg,.png,.webp"
                    onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                  />
                </div>
                <Button
                  type="submit"
                  className="w-full bg-orange-600 hover:bg-orange-700"
                  disabled={busy || policies.length === 0}
                >
                  {busy ? 'Submitting…' : 'Submit claim'}
                </Button>
              </form>
            </CardContent>
          </Card>

          <div className="space-y-4">
            {loading ? (
              <p className="text-sm text-muted-foreground">Loading your claims…</p>
            ) : claims.length === 0 ? (
              <Card>
                <CardContent className="pt-6 text-sm text-muted-foreground">
                  No claims yet. Submit your first claim using the form.
                </CardContent>
              </Card>
            ) : (
              claims.map((claim) => (
                <Card key={claim.id}>
                  <CardHeader>
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <CardTitle>{claim.claimNumber}</CardTitle>
                      <StatusBadge status={claim.status} />
                    </div>
                    <CardDescription>
                      {claim.policyNumber} · {claim.policyType} · Claimed {formatInr(claim.claimedAmount)}
                      {claim.approvedAmount != null && ` · Approved ${formatInr(claim.approvedAmount)}`}
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    <ClaimTimeline status={claim.status} />
                    <RiskBadge band={claim.aiRiskBand} score={claim.aiRiskScore} />
                    <Button variant="outline" size="sm" onClick={() => void refreshClaims()}>
                      Refresh status
                    </Button>
                  </CardContent>
                </Card>
              ))
            )}
          </div>
        </div>

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
