import { z } from 'zod'

export const loginSchema = z.object({
  email: z.email('Enter a valid email address'),
  password: z.string().min(8, 'Password must be at least 8 characters'),
})

export const registerSchema = z
  .object({
    email: z.email('Enter a valid email address'),
    fullName: z.string().min(2, 'Full name is required').max(150),
    password: z.string().min(8, 'Password must be at least 8 characters'),
    role: z.enum(['POLICYHOLDER', 'PARTNER']),
    partnerGstin: z.string().max(20).optional().or(z.literal('')),
  })
  .refine((data) => data.role !== 'PARTNER' || (data.partnerGstin ?? '').length >= 5, {
    message: 'PARTNER accounts must provide a GSTIN',
    path: ['partnerGstin'],
  })

export const submitClaimSchema = z.object({
  policyId: z.uuid('Select a policy'),
  incidentDate: z.string().min(1, 'Incident date is required'),
  claimedAmount: z.number().positive('Amount must be greater than zero'),
  documentUrl: z.url('Upload a document first'),
  idempotencyKey: z.string().min(8).max(100),
})

export const decisionSchema = z
  .object({
    claimId: z.uuid(),
    decision: z.enum(['APPROVED', 'REJECTED']),
    approvedAmount: z.number().positive().optional(),
    reasonCode: z.string().min(3, 'A reason code is mandatory for audit').max(60),
    remarks: z.string().max(1000).optional(),
  })
  .refine((data) => data.decision !== 'APPROVED' || typeof data.approvedAmount === 'number', {
    message: 'Approved amount is required when approving',
    path: ['approvedAmount'],
  })

export type LoginInput = z.infer<typeof loginSchema>
export type RegisterInput = z.infer<typeof registerSchema>
export type SubmitClaimInput = z.infer<typeof submitClaimSchema>
export type DecisionInput = z.infer<typeof decisionSchema>
