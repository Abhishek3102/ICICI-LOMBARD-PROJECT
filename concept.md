Let me explain the whole picture in plain terms.

What "subscribed to claim.submitted" + silence means

Yes — exactly. Your system is event-driven. The flow is:

Someone submits a claim on the web console (:3000).
The API saves it and publishes a message to the Kafka topic claim.submitted.
The AI triage worker — a background service running inside the API — is subscribed to that topic. The moment a message arrives, it wakes up, sends the claim to Gemini for scoring, saves the result, and publishes to claim.scored.
The adjudicator flow continues from there.
So when no one has submitted anything, the worker just sits and listens — that silence is it working correctly. It's like a phone waiting for calls: no ringing doesn't mean it's broken. The errors you saw after 10+ minutes were your Wi-Fi/VPN dropping the connection, and the app reconnects on its own.

How you run the project — this IS the normal way

You run two things in two terminals, and that's the entire daily routine:

# Terminal 1 — the backend (API + Kafka worker + AI), on http://localhost:5000
powershell -ExecutionPolicy Bypass -File scripts/run-api-local.ps1

# Terminal 2 — the web console, on http://localhost:3000
powershell -ExecutionPolicy Bypass -File scripts/run-web-local.ps1
Then open http://localhost:3000 in your browser. That's it — those two commands are your "run the project" for every work session from now on.

Why the scripts exist instead of plain commands

The API reads your .env (Kafka, Supabase, Gemini keys...), but ASP.NET needs those values under different names (Kafka__SaslPassword etc.). The script does that translation, so you never touch config by hand.
The web needs its keys in a file called .env.local with NEXT_PUBLIC_ prefixes. The script builds that file from your .env automatically (it already did — you saw "Wrote .env.local").
One-time setup is already done: pnpm is installed, .env.local is written. From the next run onward, both scripts just start instantly.
What you should do right now

Start both terminals as above, register at :3000, submit a claim, and watch Terminal 1 — within ~60 seconds you should see a Claim ... scored ... line and the claim's status change to Auto-Approved or Manual Review. That's the full pipeline proving itself end to end.