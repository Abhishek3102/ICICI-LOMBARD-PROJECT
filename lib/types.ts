export type ClaimStatus =
  | 'SUBMITTED'
  | 'AI_SCORING'
  | 'AUTO_APPROVED'
  | 'MANUAL_REVIEW'
  | 'APPROVED'
  | 'REJECTED'

export type RiskBand = 'LOW' | 'MEDIUM' | 'HIGH'

export type UserRole = 'POLICYHOLDER' | 'PARTNER' | 'ADJUDICATOR' | 'ADMIN'

export interface AuthResponse {
  token: string
  userId: string
  email: string
  fullName: string
  role: UserRole
}

export interface PolicyDto {
  id: string
  policyNumber: string
  policyType: 'HEALTH' | 'MOTOR'
  sumInsured: number
  remainingBalance: number
  status: 'ACTIVE' | 'EXPIRED' | 'SUSPENDED'
  validFrom: string
  validTo: string
}

export interface ExtractedLineItem {
  description: string
  amount: number
}

export interface AiReasoning {
  score: number
  band: RiskBand
  confidence: number
  anomalies: string[]
  extractedItems: ExtractedLineItem[]
  reasoning: string
  engine: string
}

export interface ClaimResponse {
  id: string
  claimNumber: string
  policyId: string
  policyNumber: string
  policyType: string
  claimedAmount: number
  approvedAmount: number | null
  documentUrl: string
  status: ClaimStatus
  aiRiskScore: number | null
  aiRiskBand: RiskBand | null
  aiReasoning: string | null
  createdAt: string
}

export interface ReviewQueueItem {
  id: string
  claimNumber: string
  policyNumber: string
  policyType: string
  holderName: string
  claimedAmount: number
  remainingBalance: number
  aiRiskScore: number | null
  aiRiskBand: RiskBand | null
  aiReasoning: string | null
  documentUrl: string
  createdAt: string
}

export interface AuditLogEntry {
  id: string
  claimId: string
  claimNumber: string | null
  actorType: string
  actorName: string | null
  previousStatus: string | null
  newStatus: string
  remarks: string
  kafkaOffset: string | null
  createdAt: string
}

export function parseAiReasoning(raw: string | null): AiReasoning | null {
  if (!raw) return null
  try {
    return JSON.parse(raw) as AiReasoning
  } catch {
    return null
  }
}

export function formatInr(amount: number): string {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 0,
  }).format(amount)
}
