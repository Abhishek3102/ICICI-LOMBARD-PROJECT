'use client'

import { useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'

import { NavBar } from '@/components/nav-bar'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { api, ApiError } from '@/lib/api-client'
import { clearAuth, getAuth } from '@/lib/auth'
import { formatInr, type ClaimResponse, type PolicyDto } from '@/lib/types'
import { submitClaimSchema } from '@/lib/zod-schemas'

export default function PartnerPage() {
  const router = useRouter()
  const [policyNumber, setPolicyNumber] = useState('')
  const [policy, setPolicy] = useState<PolicyDto | null>(null)
  const [incidentDate, setIncidentDate] = useState('')
  const [amount, setAmount] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [submitted, setSubmitted] = useState<ClaimResponse | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    const auth = getAuth()
    if (!auth) {
      router.push('/login')
      return
    }
    if (auth.role !== 'PARTNER') {
      router.push(auth.role === 'POLICYHOLDER' ? '/portal' : '/ops/adjudicate')
    }
  }, [router])

  async function handleLookup() {
    setError(null)
    setPolicy(null)
    if (!policyNumber.trim()) {
      setError('Enter a policy number first.')
      return
    }
    setBusy(true)
    try {
      setPolicy(await api.lookupPolicy(policyNumber.trim()))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Policy lookup failed.')
    } finally {
      setBusy(false)
    }
  }

  async function handlePreAuth(event: React.FormEvent) {
    event.preventDefault()
    setError(null)

    if (!policy) {
      setError('Look up a policy first.')
      return
    }
    if (!file) {
      setError('Attach the estimate or discharge summary (PDF/JPEG/PNG, max 10 MB).')
      return
    }

    const parsed = submitClaimSchema.safeParse({
      policyId: policy.id,
      incidentDate,
      claimedAmount: Number(amount),
      documentUrl: 'pending-upload',
      idempotencyKey: crypto.randomUUID(),
    })
    if (!parsed.success) {
      setError(parsed.error.issues[0]?.message ?? 'Invalid pre-authorization data')
      return
    }

    setBusy(true)
    try {
      const upload = await api.uploadDocument(file)
      const idempotencyKey = crypto.randomUUID()
      const claim = await api.submitClaim(
        {
          policyId: policy.id,
          incidentDate: parsed.data.incidentDate,
          claimedAmount: parsed.data.claimedAmount,
          documentUrl: upload.documentUrl,
          idempotencyKey,
        },
        idempotencyKey,
      )
      setSubmitted(claim)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Pre-authorization failed. Do not retry blindly — the idempotency key protects you from double submission.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="min-h-screen">
      <NavBar />
      <main className="mx-auto max-w-4xl px-4 py-8">
        <h1 className="text-2xl font-semibold tracking-tight">Cashless pre-authorization desk</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Network hospital / garage workflow: look up the policy, attach the estimate, submit once.
        </p>

        {error && (
          <p role="alert" className="mt-4 rounded-md bg-red-500/10 px-3 py-2 text-sm text-red-600 dark:text-red-400">
            {error}
          </p>
        )}

        {submitted ? (
          <Card className="mt-6">
            <CardHeader>
              <CardTitle>Pre-authorization received: {submitted.claimNumber}</CardTitle>
              <CardDescription>
                The AI triage engine is scoring this claim now. Low-risk claims under ₹10,000 are
                auto-approved; everything else lands in the adjudicator queue.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <p className="text-sm">
                Claimed amount: <span className="font-mono">{formatInr(submitted.claimedAmount)}</span>
              </p>
              <Button variant="outline" size="sm" onClick={() => setSubmitted(null)}>
                Submit another pre-authorization
              </Button>
            </CardContent>
          </Card>
        ) : (
          <div className="mt-6 space-y-6">
            <Card>
              <CardHeader>
                <CardTitle>Step 1 · Policy lookup</CardTitle>
                <CardDescription>Verify the patient&apos;s / vehicle owner&apos;s coverage.</CardDescription>
              </CardHeader>
              <CardContent>
                <div className="flex flex-col gap-2 sm:flex-row">
                  <Input
                    value={policyNumber}
                    onChange={(e) => setPolicyNumber(e.target.value)}
                    placeholder="POL-2024-XXXXXX"
                    aria-label="Policy number"
                  />
                  <Button onClick={() => void handleLookup()} disabled={busy} variant="outline">
                    Look up
                  </Button>
                </div>
                {policy && (
                  <dl className="mt-4 grid grid-cols-2 gap-2 text-sm sm:grid-cols-4">
                    <div>
                      <dt className="text-muted-foreground">Type</dt>
                      <dd className="font-medium">{policy.policyType}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">Status</dt>
                      <dd className="font-medium">{policy.status}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">Sum insured</dt>
                      <dd className="font-mono">{formatInr(policy.sumInsured)}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">Remaining</dt>
                      <dd className="font-mono">{formatInr(policy.remainingBalance)}</dd>
                    </div>
                  </dl>
                )}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Step 2 · Pre-authorization request</CardTitle>
                <CardDescription>
                  Every request carries a unique idempotency key — a network retry can never double-submit.
                </CardDescription>
              </CardHeader>
              <CardContent>
                <form onSubmit={handlePreAuth} className="space-y-4" noValidate>
                  <div className="grid gap-4 sm:grid-cols-2">
                    <div className="space-y-1.5">
                      <Label htmlFor="incidentDate">Incident / admission date</Label>
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
                      <Label htmlFor="amount">Estimated amount (₹)</Label>
                      <Input
                        id="amount"
                        type="number"
                        min={1}
                        step="0.01"
                        value={amount}
                        onChange={(e) => setAmount(e.target.value)}
                        placeholder="48000"
                        required
                      />
                    </div>
                  </div>
                  <div className="space-y-1.5">
                    <Label htmlFor="document">Estimate / discharge summary</Label>
                    <Input
                      id="document"
                      type="file"
                      accept=".pdf,.jpg,.jpeg,.png,.webp"
                      onChange={(e) => setFile(e.target.files?.[0] ?? null)}
                    />
                  </div>
                  <Button
                    type="submit"
                    className="bg-orange-600 hover:bg-orange-700"
                    disabled={busy || !policy}
                  >
                    {busy ? 'Submitting…' : 'Submit pre-authorization'}
                  </Button>
                </form>
              </CardContent>
            </Card>
          </div>
        )}

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
