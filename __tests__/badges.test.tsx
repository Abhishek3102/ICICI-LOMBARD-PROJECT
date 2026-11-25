import { render, screen } from '@testing-library/react'

import { RiskBadge } from '@/components/risk-badge'
import { StatusBadge } from '@/components/status-badge'

describe('StatusBadge', () => {
  it('renders a human label for MANUAL_REVIEW', () => {
    render(<StatusBadge status="MANUAL_REVIEW" />)
    expect(screen.getByText('Manual Review')).toBeInTheDocument()
  })

  it('renders a success style for APPROVED', () => {
    render(<StatusBadge status="APPROVED" />)
    expect(screen.getByText('Approved')).toBeInTheDocument()
  })
})

describe('RiskBadge', () => {
  it('shows the score when provided', () => {
    render(<RiskBadge band="HIGH" score={82} />)
    expect(screen.getByText('High Risk · 82/100')).toBeInTheDocument()
  })

  it('explains when a claim has not been scored yet', () => {
    render(<RiskBadge band={null} />)
    expect(screen.getByText('Not scored yet')).toBeInTheDocument()
  })
})
