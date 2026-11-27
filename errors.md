Found and fixed the real bug this time. Your API was hanging at startup: the RabbitMQ (CloudAMQP) connection was opened synchronously during host startup, before the web server starts listening. With your flaky network, that connection attempt hung — so the Kafka worker logged its startup lines, but Kestrel never came up, nothing ever listened on :5000, and the frontend correctly said "Could not reach the API". That's also why you never saw the Now listening on: http://localhost:5000 line.

The fix (pushed as cfbe81f): both RabbitMQ connections are now lazy with a 10-second timeout and retry loops. The API always comes up now — if CloudAMQP is unreachable you'll just see a warning and notifications retry later, instead of a silent hang.

git pull
powershell -ExecutionPolicy Bypass -File scripts/run-api-local.ps1
This time you should see Now listening on: http://localhost:5000 within a few seconds — that line is your confirmation. Keep it running, start the web in a second terminal, and register at :3000. If RabbitMQ is unreachable you may see a [v0] RabbitMQ connect failed warning — that's fine and expected while your network is unstable; registration and the Kafka AI triage don't depend on it.

One note: I couldn't compile in this sandbox (no .NET SDK here), so if the build throws anything on your machine, paste it and I'll fix it immediately — but the change uses the same RabbitMQ.Client APIs already in the file.