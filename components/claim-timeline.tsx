import { cn } from '@/lib/utils'
import type { ClaimStatus } from '@/lib/types'

const steps: { key: ClaimStatus; label: string }[] = [
  { key: 'SUBMITTED', label: 'Submitted' },
  { key: 'AI_SCORING', label: 'AI Triage' },
  { key: 'MANUAL_REVIEW', label: 'Review' },
  { key: 'APPROVED', label: 'Decision' },
]

const autoPath: { key: ClaimStatus; label: string }[] = [
  { key: 'SUBMITTED', label: 'Submitted' },
  { key: 'AI_SCORING', label: 'AI Triage' },
  { key: 'AUTO_APPROVED', label: 'Auto-Approved' },
]

const order: Record<ClaimStatus, number> = {
  SUBMITTED: 0,
  AI_SCORING: 1,
  AUTO_APPROVED: 2,
  MANUAL_REVIEW: 2,
  APPROVED: 3,
  REJECTED: 3,
}

export function ClaimTimeline({ status }: { status: ClaimStatus }) {
  const path = status === 'AUTO_APPROVED' ? autoPath : steps
  const currentIndex = order[status]

  return (
    <ol className="flex items-center gap-2" aria-label="Claim progress">
      {path.map((step, index) => {
        const reached = index <= currentIndex
        const isTerminal = index === path.length - 1
        return (
          <li key={step.key} className="flex items-center gap-2">
            <div className="flex items-center gap-1.5">
              <span
                aria-hidden
                className={cn(
                  'flex h-5 w-5 items-center justify-center rounded-full border text-[10px] font-semibold',
                  reached
                    ? status === 'REJECTED' && isTerminal
                      ? 'border-red-500 bg-red-500 text-white'
                      : 'border-orange-500 bg-orange-500 text-white'
                    : 'border-border text-muted-foreground',
                )}
              >
                {index + 1}
              </span>
              <span
                className={cn(
                  'text-xs',
                  reached ? 'font-medium text-foreground' : 'text-muted-foreground',
                )}
              >
                {step.label}
              </span>
            </div>
            {index < path.length - 1 && (
              <span
                aria-hidden
                className={cn('h-px w-6', index < currentIndex ? 'bg-orange-500' : 'bg-border')}
              />
            )}
          </li>
        )
      })}
    </ol>
  )
}
