import { getToken } from './auth'
import type {
  AuditLogEntry,
  AuthResponse,
  ClaimResponse,
  PolicyDto,
  ReviewQueueItem,
} from './types'

export const API_BASE =
  process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:5000'

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  const token = getToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)
  if (init.body && !(init.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(`${API_BASE}${path}`, { ...init, headers })

  if (!response.ok) {
    let detail = `Request failed with status ${response.status}`
    try {
      const body = (await response.json()) as { detail?: string }
      if (body.detail) detail = body.detail
    } catch {
      // non-JSON error body
    }
    throw new ApiError(response.status, detail)
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

export const api = {
  login: (email: string, password: string) =>
    request<AuthResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),

  register: (payload: {
    email: string
    fullName: string
    password: string
    role: string
    partnerGstin?: string
    bootstrapSecret?: string
  }) =>
    request<AuthResponse>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  myPolicies: () => request<PolicyDto[]>('/api/policies/mine'),

  lookupPolicy: (number: string) =>
    request<PolicyDto>(`/api/policies/lookup?number=${encodeURIComponent(number)}`),

  myClaims: () => request<ClaimResponse[]>('/api/claims/mine'),

  getClaim: (id: string) => request<ClaimResponse>(`/api/claims/${id}`),

  submitClaim: (payload: {
    policyId: string
    incidentDate: string
    claimedAmount: number
    documentUrl: string
    idempotencyKey: string
  }, idempotencyKey: string) =>
    request<ClaimResponse>('/api/claims', {
      method: 'POST',
      body: JSON.stringify(payload),
      headers: { 'X-Idempotency-Key': idempotencyKey },
    }),

  uploadDocument: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return request<{ documentUrl: string; path: string }>('/api/documents/upload', {
      method: 'POST',
      body: form,
    })
  },

  reviewQueue: (status = 'MANUAL_REVIEW') =>
    request<ReviewQueueItem[]>(`/api/adjudication/queue?status=${status}`),

  decide: (payload: {
    claimId: string
    decision: 'APPROVED' | 'REJECTED'
    approvedAmount?: number
    reasonCode: string
    remarks?: string
  }) =>
    request<ClaimResponse>('/api/adjudication/decide', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  auditLogs: (limit = 100) =>
    request<AuditLogEntry[]>(`/api/audit/logs?limit=${limit}`),

  claimAuditLogs: (claimId: string) =>
    request<AuditLogEntry[]>(`/api/audit/claims/${claimId}/logs`),
}
