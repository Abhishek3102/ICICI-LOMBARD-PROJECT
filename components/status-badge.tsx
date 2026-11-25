import { Badge } from '@/components/ui/badge'
import type { ClaimStatus } from '@/lib/types'

const config: Record<ClaimStatus, { label: string; variant: 'secondary' | 'success' | 'warning' | 'danger' | 'outline' }> = {
  SUBMITTED: { label: 'Submitted', variant: 'secondary' },
  AI_SCORING: { label: 'AI Scoring', variant: 'secondary' },
  AUTO_APPROVED: { label: 'Auto-Approved', variant: 'success' },
  MANUAL_REVIEW: { label: 'Manual Review', variant: 'warning' },
  APPROVED: { label: 'Approved', variant: 'success' },
  REJECTED: { label: 'Rejected', variant: 'danger' },
}

export function StatusBadge({ status }: { status: ClaimStatus }) {
  const { label, variant } = config[status] ?? { label: status, variant: 'outline' as const }
  return <Badge variant={variant}>{label}</Badge>
}
