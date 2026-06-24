# Lab: Telemetry & tracing for agents (OpenTelemetry)

Trace an agent turn (model call + tool call) with the **GenAI semantic
conventions** and emit a token-usage metric. Console export works with no Azure;
Application Insights export lights up the trace tree in the Foundry portal.

## Files
| File | Purpose |
|------|---------|
| `telemetry.py` | Configures a TracerProvider + MeterProvider, traces a bakery turn, exports spans/metrics. |

## Run offline (console exporter)
```bash
pip install -r requirements.txt
python telemetry.py
```
You'll see a parent `chat frankies-bakery` span with a child `execute_tool
list_products` span, GenAI attributes (`gen_ai.system`, `gen_ai.request.model`,
`gen_ai.usage.*`), and a `gen_ai.client.token.usage` metric.

## Stream to Azure Monitor / Foundry
```bash
export APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=...;IngestionEndpoint=..."
python telemetry.py
```
Open your Foundry project's **Tracing** tab to see the spans. To auto-record chat
content set `AZURE_TRACING_GEN_AI_CONTENT_RECORDING_ENABLED=true`.

## Key idea
Standard OpenTelemetry + GenAI conventions mean the same spans render in the
Foundry portal, Azure Monitor, Grafana, or any OTel backend.
