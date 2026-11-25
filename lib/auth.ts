import type { AuthResponse, UserRole } from './types'

const AUTH_KEY = 'claimguard.auth'

export function saveAuth(auth: AuthResponse): void {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(AUTH_KEY, JSON.stringify(auth))
}

export function getAuth(): AuthResponse | null {
  if (typeof window === 'undefined') return null
  const raw = window.localStorage.getItem(AUTH_KEY)
  if (!raw) return null
  try {
    return JSON.parse(raw) as AuthResponse
  } catch {
    return null
  }
}

export function getToken(): string | null {
  return getAuth()?.token ?? null
}

export function clearAuth(): void {
  if (typeof window === 'undefined') return
  window.localStorage.removeItem(AUTH_KEY)
}

export function hasRole(role: UserRole | undefined, allowed: UserRole[]): boolean {
  return !!role && allowed.includes(role)
}
