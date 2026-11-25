'use client'

import Link from 'next/link'
import { useRouter } from 'next/navigation'
import { useState } from 'react'

import { NavBar } from '@/components/nav-bar'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { api, ApiError } from '@/lib/api-client'
import { saveAuth } from '@/lib/auth'
import type { UserRole } from '@/lib/types'
import { loginSchema, registerSchema } from '@/lib/zod-schemas'

const homeByRole: Record<UserRole, string> = {
  POLICYHOLDER: '/portal',
  PARTNER: '/partner',
  ADJUDICATOR: '/ops/adjudicate',
  ADMIN: '/ops/adjudicate',
}

export default function LoginPage() {
  const router = useRouter()
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [fullName, setFullName] = useState('')
  const [role, setRole] = useState<'POLICYHOLDER' | 'PARTNER'>('POLICYHOLDER')
  const [partnerGstin, setPartnerGstin] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  function routeByRole(userRole: UserRole) {
    router.push(homeByRole[userRole] ?? '/portal')
  }

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault()
    setError(null)

    if (mode === 'login') {
      const parsed = loginSchema.safeParse({ email, password })
      if (!parsed.success) {
        setError(parsed.error.issues[0]?.message ?? 'Invalid input')
        return
      }
      setBusy(true)
      try {
        const auth = await api.login(parsed.data.email, parsed.data.password)
        saveAuth(auth)
        routeByRole(auth.role)
      } catch (err) {
        setError(err instanceof ApiError ? err.message : 'Could not reach the API. Is the backend running?')
      } finally {
        setBusy(false)
      }
      return
    }

    const parsed = registerSchema.safeParse({
      email,
      fullName,
      password,
      role,
      partnerGstin: partnerGstin || undefined,
    })
    if (!parsed.success) {
      setError(parsed.error.issues[0]?.message ?? 'Invalid input')
      return
    }
    setBusy(true)
    try {
      const auth = await api.register({
        email: parsed.data.email,
        fullName: parsed.data.fullName,
        password: parsed.data.password,
        role: parsed.data.role,
        partnerGstin: parsed.data.partnerGstin || undefined,
      })
      saveAuth(auth)
      routeByRole(auth.role)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Could not reach the API. Is the backend running?')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="min-h-screen">
      <NavBar />
      <main className="mx-auto flex max-w-md flex-col justify-center px-4 py-16">
        <Card>
          <CardHeader>
            <CardTitle>{mode === 'login' ? 'Sign in' : 'Create an account'}</CardTitle>
            <CardDescription>
              {mode === 'login'
                ? 'Use your ClaimGuard credentials to open your console.'
                : 'Self-signup is available for policyholders and partners.'}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSubmit} className="space-y-4" noValidate>
              {mode === 'register' && (
                <>
                  <div className="space-y-1.5">
                    <Label htmlFor="fullName">Full name</Label>
                    <Input
                      id="fullName"
                      value={fullName}
                      onChange={(e) => setFullName(e.target.value)}
                      autoComplete="name"
                      required
                    />
                  </div>
                  <div className="space-y-1.5">
                    <Label htmlFor="role">Account type</Label>
                    <select
                      id="role"
                      value={role}
                      onChange={(e) => setRole(e.target.value as 'POLICYHOLDER' | 'PARTNER')}
                      className="h-9 w-full rounded-md border border-input bg-transparent px-3 text-sm"
                    >
                      <option value="POLICYHOLDER">Policyholder</option>
                      <option value="PARTNER">Partner (Hospital / Garage)</option>
                    </select>
                  </div>
                  {role === 'PARTNER' && (
                    <div className="space-y-1.5">
                      <Label htmlFor="gstin">GSTIN</Label>
                      <Input
                        id="gstin"
                        value={partnerGstin}
                        onChange={(e) => setPartnerGstin(e.target.value)}
                        placeholder="29ABCDE1234F1Z5"
                      />
                    </div>
                  )}
                </>
              )}

              <div className="space-y-1.5">
                <Label htmlFor="email">Email</Label>
                <Input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  autoComplete="email"
                  required
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="password">Password</Label>
                <Input
                  id="password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
                  minLength={8}
                  required
                />
              </div>

              {error && (
                <p role="alert" className="rounded-md bg-red-500/10 px-3 py-2 text-sm text-red-600 dark:text-red-400">
                  {error}
                </p>
              )}

              <Button type="submit" className="w-full bg-orange-600 hover:bg-orange-700" disabled={busy}>
                {busy ? 'Please wait…' : mode === 'login' ? 'Sign in' : 'Create account'}
              </Button>
            </form>

            <p className="mt-4 text-center text-sm text-muted-foreground">
              {mode === 'login' ? 'New to ClaimGuard? ' : 'Already registered? '}
              <button
                type="button"
                onClick={() => {
                  setMode(mode === 'login' ? 'register' : 'login')
                  setError(null)
                }}
                className="font-medium text-orange-600 underline-offset-4 hover:underline dark:text-orange-400"
              >
                {mode === 'login' ? 'Create an account' : 'Sign in instead'}
              </button>
            </p>
          </CardContent>
        </Card>

        <p className="mt-6 text-center text-xs text-muted-foreground">
          <Link href="/" className="underline-offset-4 hover:underline">
            ← Back to overview
          </Link>
        </p>
      </main>
    </div>
  )
}
