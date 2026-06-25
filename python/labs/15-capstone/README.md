# Lab 7: Capstone

Combine what you built across the workshop into one agent (or small multi-agent
system). `capstone.py` is a starter: pick a scenario, add the tools it needs,
wire in guardrails + telemetry + an eval gate, and iterate.

Every idea below builds on the **Frankie's Bakery / GiftBot** scenario and reuses
labs you already have: `agent-function`, `filesystem-rag`, `multi-agent-*`,
`mcp-server`, `guardrails`, `telemetry`, `evaluations`, `evaluations-tests`.

## Creative capstone ideas

### 1. Frankie's Bakery concierge (full stack)
One agent that takes orders end-to-end: discovery (`search_products`), ordering
(`place_order` via your **MCP server**), wrapped in the **guardrails** pipeline,
traced with **telemetry**, and protected by an **evaluations-tests** CI gate.
*Stretch:* add a Bing-grounded "what pairs with this cake?" answer.

### 2. GiftBot multi-agent gift planner
Sequential pipeline: an **intake** agent extracts budget/occasion/recipient, a
**retrieval** agent searches the gift corpus (`filesystem-rag`), a **shopper**
agent places bakery orders through the MCP server, and a **writer** agent drafts
a gift note. Add a guardrail that blocks PII leakage in the note.

### 3. "Red team your bakery agent" safety lab
Build an adversarial agent that generates jailbreak / injection / data-exfil
prompts, run them through the **guardrails** pipeline, and score pass/fail with
**evaluations-tests**. Deliverable: a safety scorecard + the prompts that slipped
through. Most creative escape wins.

### 4. MCP marketplace
Build two MCP servers (bakery catalog + a new "loyalty points" server) and an
agent that orchestrates both with per-tool `require_approval`. Demonstrate the
approval workflow blocking a risky `redeem_points` call.

### 5. Observability dashboard
Instrument a multi-turn conversation with **telemetry**, export to Application
Insights, and build a KQL query / workbook showing latency, token cost per turn,
and tool-call frequency. Find and fix the slowest span.

### 6. Self-improving FAQ agent
Agent answers bakery FAQs; every answer is graded by **evaluations** (or the
offline `evaluators.py`). Answers below threshold are logged, and you regenerate
the system prompt to lift the aggregate F1 above the release floor.

### 7. Voice/ordering kiosk (stretch)
Wrap the concierge agent in a simple CLI "kiosk" loop with conversation memory,
a cart, an order total, and a guardrail that refuses non-bakery requests.

### 8. Cross-channel support triage
Concurrent agents (`multi-agent-concurrent`) classify an incoming message
(complaint / order / allergen question) and route to the right specialist, with
telemetry spans showing the fan-out/fan-in.

## What a strong capstone shows
- At least **two tools** (MCP server + one of function/search/OpenAPI/Bing).
- **Guardrails** on input and output.
- **Telemetry** spans for the agent turn and tool calls.
- An **eval gate** (pytest or Foundry) proving quality/safety before "shipping".
- A short README: scenario, architecture sketch, how to run, what you'd do next.
