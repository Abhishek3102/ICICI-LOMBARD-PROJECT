'use client'

import Link from 'next/link'
import { useRouter } from 'next/navigation'

import { Button } from '@/components/ui/button'
import { clearAuth, getAuth } from '@/lib/auth'
import type { UserRole } from '@/lib/types'


const linksByRole: Record<UserRole, { href: string; label: string }[]> = {
  POLICYHOLDER: [{ href: '/portal', label: 'My Claims' }],
  PARTNER: [{ href: '/partner', label: 'Cashless Desk' }],
  ADJUDICATOR: [
    { href: '/ops/adjudicate', label: 'Adjudicate' },
    { href: '/ops/audit', label: 'Audit Trail' },
  ],
  ADMIN: [
    { href: '/ops/adjudicate', label: 'Adjudicate' },
    { href: '/ops/audit', label: 'Audit Trail' },
  ],
}

export function NavBar() {
  const router = useRouter()
  const auth = getAuth()
  const links = auth ? linksByRole[auth.role] ?? [] : []

  return (
    <header className="border-b border-border bg-background/95 backdrop-blur">
      <div className="mx-auto flex h-14 max-w-6xl items-center justify-between px-4">
        <Link href="/" className="flex items-center gap-2 font-semibold tracking-tight">
          <span className="flex h-6 w-6 items-center justify-center rounded bg-orange-500 text-xs font-bold text-white">
            CG
          </span>
          ClaimGuard AI
        </Link>

        <nav className="flex items-center gap-1 sm:gap-4" aria-label="Main">
          {links.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              className="rounded-md px-2 py-1 text-sm text-muted-foreground transition-colors hover:text-foreground"
            >
              {link.label}
            </Link>
          ))}
          {auth ? (
            <div className="flex items-center gap-2">
              <span className="hidden text-xs text-muted-foreground sm:inline">
                {auth.fullName} · {auth.role}
              </span>
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  clearAuth()
                  router.push('/login')
                }}
              >
                Sign out
              </Button>
            </div>
          ) : (
            <Link
              href="/login"
              className="inline-flex h-8 items-center rounded-md bg-primary px-3 text-sm font-medium text-primary-foreground transition-colors hover:bg-primary/90"
            >
              Sign in
            </Link>
          )}
        </nav>
      </div>
    </header>
  )
}
