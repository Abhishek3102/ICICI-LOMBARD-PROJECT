import { render, screen } from '@testing-library/react'

import { ClaimTimeline } from '@/components/claim-timeline'

describe('ClaimTimeline', () => {
  it('shows the auto-approval fast path for AUTO_APPROVED claims', () => {
    render(<ClaimTimeline status="AUTO_APPROVED" />)
    expect(screen.getByText('Auto-Approved')).toBeInTheDocument()
    expect(screen.queryByText('Review')).not.toBeInTheDocument()
  })

  it('shows the manual-review path for MANUAL_REVIEW claims', () => {
    render(<ClaimTimeline status="MANUAL_REVIEW" />)
    expect(screen.getByText('Review')).toBeInTheDocument()
    expect(screen.queryByText('Auto-Approved')).not.toBeInTheDocument()
  })

  it('always starts at Submitted', () => {
    render(<ClaimTimeline status="APPROVED" />)
    expect(screen.getByText('Submitted')).toBeInTheDocument()
  })
})
