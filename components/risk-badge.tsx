import { Badge } from '@/components/ui/badge'
import type { RiskBand } from '@/lib/types'

const config: Record<RiskBand, { label: string; variant: 'success' | 'warning' | 'danger' }> = {
  LOW: { label: 'Low Risk', variant: 'success' },
  MEDIUM: { label: 'Medium Risk', variant: 'warning' },
  HIGH: { label: 'High Risk', variant: 'danger' },
}

export function RiskBadge({ band, score }: { band: RiskBand | null; score?: number | null }) {
  if (!band) return <span className="text-xs text-muted-foreground">Not scored yet</span>
  const { label, variant } = config[band]
  return (
    <Badge variant={variant}>
      {label}
      {typeof score === 'number' ? ` · ${score}/100` : ''}
    </Badge>
  )
}
