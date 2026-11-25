import { decisionSchema, loginSchema, submitClaimSchema } from '@/lib/zod-schemas'

describe('loginSchema', () => {
  it('accepts valid credentials', () => {
    const result = loginSchema.safeParse({ email: 'user@test.in', password: 'password123' })
    expect(result.success).toBe(true)
  })

  it('rejects short passwords', () => {
    const result = loginSchema.safeParse({ email: 'user@test.in', password: 'short' })
    expect(result.success).toBe(false)
  })

  it('rejects invalid emails', () => {
    const result = loginSchema.safeParse({ email: 'not-an-email', password: 'password123' })
    expect(result.success).toBe(false)
  })
})

describe('submitClaimSchema', () => {
  const valid = {
    policyId: '550e8400-e29b-41d4-a716-446655440000',
    incidentDate: '2026-11-20',
    claimedAmount: 25000,
    documentUrl: 'https://storage.example.com/invoice.pdf',
    idempotencyKey: 'a1b2c3d4e5f6',
  }

  it('accepts a valid claim payload', () => {
    expect(submitClaimSchema.safeParse(valid).success).toBe(true)
  })

  it('rejects non-positive amounts', () => {
    expect(submitClaimSchema.safeParse({ ...valid, claimedAmount: 0 }).success).toBe(false)
  })

  it('rejects short idempotency keys', () => {
    expect(submitClaimSchema.safeParse({ ...valid, idempotencyKey: 'abc' }).success).toBe(false)
  })
})

describe('decisionSchema', () => {
  const base = { claimId: '550e8400-e29b-41d4-a716-446655440000', reasonCode: 'VERIFIED_OK' }

  it('requires an approved amount when approving', () => {
    const result = decisionSchema.safeParse({ ...base, decision: 'APPROVED', approvedAmount: 1000 })
    expect(result.success).toBe(true)
    expect(decisionSchema.safeParse({ ...base, decision: 'APPROVED' }).success).toBe(false)
  })

  it('allows rejection without an amount', () => {
    expect(decisionSchema.safeParse({ ...base, decision: 'REJECTED' }).success).toBe(true)
  })

  it('requires a reason code for the audit trail', () => {
    expect(
      decisionSchema.safeParse({ claimId: base.claimId, decision: 'REJECTED', reasonCode: '' }).success,
    ).toBe(false)
  })
})
