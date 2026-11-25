import { formatInr, parseAiReasoning } from '@/lib/types'

/**
 * Side-by-side "1-Minute Co-Pilot" view: what the AI extracted from the invoice
 * versus the policy limits, plus the anomaly list that drove the risk score.
 */
export function InvoiceViewer({
  aiReasoning,
  claimedAmount,
  remainingBalance,
  documentUrl,
}: {
  aiReasoning: string | null
  claimedAmount: number
  remainingBalance: number
  documentUrl: string
}) {
  const reasoning = parseAiReasoning(aiReasoning)
  const items = reasoning?.extractedItems ?? []
  const total = items.reduce((sum, item) => sum + item.amount, 0)

  return (
    <div className="grid gap-4 md:grid-cols-2">
      <div className="rounded-lg border border-border p-4">
        <h4 className="mb-3 text-sm font-semibold">AI-extracted line items</h4>
        {items.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No structured line items were extracted for this document.
          </p>
        ) : (
          <ul className="space-y-2">
            {items.map((item, index) => (
              <li key={index} className="flex items-center justify-between text-sm">
                <span className="text-foreground">{item.description}</span>
                <span className="font-mono text-xs">{formatInr(item.amount)}</span>
              </li>
            ))}
            <li className="flex items-center justify-between border-t border-border pt-2 text-sm font-medium">
              <span>Extracted total</span>
              <span className="font-mono text-xs">{formatInr(total)}</span>
            </li>
            <li className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">Claimed amount</span>
              <span className="font-mono text-xs">{formatInr(claimedAmount)}</span>
            </li>
          </ul>
        )}
      </div>

      <div className="rounded-lg border border-border p-4">
        <h4 className="mb-3 text-sm font-semibold">Policy limits &amp; anomalies</h4>
        <p className="mb-3 text-sm text-muted-foreground">
          Remaining balance: <span className="font-mono">{formatInr(remainingBalance)}</span>
        </p>
        {reasoning?.anomalies?.length ? (
          <ul className="mb-3 space-y-1.5">
            {reasoning.anomalies.map((anomaly, index) => (
              <li key={index} className="flex gap-2 text-sm text-amber-600 dark:text-amber-400">
                <span aria-hidden>&#9888;</span>
                <span>{anomaly}</span>
              </li>
            ))}
          </ul>
        ) : (
          <p className="mb-3 text-sm text-emerald-600 dark:text-emerald-400">
            No anomalies flagged by the AI engine.
          </p>
        )}
        {reasoning?.reasoning && (
          <p className="text-xs leading-relaxed text-muted-foreground">{reasoning.reasoning}</p>
        )}
        <a
          href={documentUrl}
          target="_blank"
          rel="noreferrer"
          className="mt-3 inline-block text-xs font-medium text-orange-600 underline-offset-4 hover:underline dark:text-orange-400"
        >
          Open source document &rarr;
        </a>
      </div>
    </div>
  )
}
