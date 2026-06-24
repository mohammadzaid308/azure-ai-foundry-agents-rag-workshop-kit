"""Lab: end-to-end telemetry for a bakery agent run with OpenTelemetry.

This lab traces an agent turn (model call + tool call) using the GenAI
semantic conventions, plus a token-usage metric. By default everything is
exported to the CONSOLE so you can see the spans without any Azure resource.
Set APPLICATIONINSIGHTS_CONNECTION_STRING to also stream to Azure Monitor and
view the trace tree in the Foundry portal.

Offline:  python telemetry.py
Azure:    APPLICATIONINSIGHTS_CONNECTION_STRING=... python telemetry.py
"""
import os
import time

from opentelemetry import trace, metrics
from opentelemetry.sdk.resources import Resource
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import SimpleSpanProcessor, ConsoleSpanExporter
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import ConsoleMetricExporter, PeriodicExportingMetricReader

SERVICE_NAME = "frankies-bakery-agent"
MODEL = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4o")


def configure_telemetry():
    """Wire a TracerProvider + MeterProvider. Console always on; Azure if configured."""
    resource = Resource.create({"service.name": SERVICE_NAME})

    tracer_provider = TracerProvider(resource=resource)
    tracer_provider.add_span_processor(SimpleSpanProcessor(ConsoleSpanExporter()))

    metric_readers = [PeriodicExportingMetricReader(ConsoleMetricExporter(), export_interval_millis=600000)]
    meter_provider = MeterProvider(resource=resource, metric_readers=metric_readers)

    conn = os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING")
    if conn:
        try:
            from azure.monitor.opentelemetry.exporter import (
                AzureMonitorTraceExporter,
                AzureMonitorMetricExporter,
            )
            tracer_provider.add_span_processor(
                SimpleSpanProcessor(AzureMonitorTraceExporter.from_connection_string(conn))
            )
            print("Azure Monitor exporter enabled.\n")
        except ImportError:
            print("Install azure-monitor-opentelemetry-exporter to stream to Azure.\n")

    trace.set_tracer_provider(tracer_provider)
    metrics.set_meter_provider(meter_provider)
    return tracer_provider, meter_provider


def run_traced_turn(tracer, token_counter, user_query):
    """Simulate one agent turn and emit GenAI-convention spans + token metric."""
    with tracer.start_as_current_span("chat frankies-bakery", kind=trace.SpanKind.CLIENT) as chat:
        chat.set_attribute("gen_ai.system", "az.ai.foundry")
        chat.set_attribute("gen_ai.operation.name", "chat")
        chat.set_attribute("gen_ai.request.model", MODEL)
        chat.set_attribute("gen_ai.prompt", user_query)

        # Child span: the agent decides to call the catalog tool.
        with tracer.start_as_current_span("execute_tool list_products") as tool:
            tool.set_attribute("gen_ai.operation.name", "execute_tool")
            tool.set_attribute("gen_ai.tool.name", "list_products")
            time.sleep(0.05)
            tool.set_attribute("gen_ai.tool.result_count", 7)

        answer = "We have sourdough, rye, ciabatta and more, all baked fresh today."
        input_tokens, output_tokens = 42, 18
        chat.set_attribute("gen_ai.usage.input_tokens", input_tokens)
        chat.set_attribute("gen_ai.usage.output_tokens", output_tokens)
        chat.set_attribute("gen_ai.completion", answer)

        token_counter.add(input_tokens, {"gen_ai.token.type": "input", "gen_ai.request.model": MODEL})
        token_counter.add(output_tokens, {"gen_ai.token.type": "output", "gen_ai.request.model": MODEL})
        return answer


if __name__ == "__main__":
    tracer_provider, meter_provider = configure_telemetry()
    tracer = trace.get_tracer(__name__)
    meter = metrics.get_meter(__name__)
    token_counter = meter.create_counter("gen_ai.client.token.usage", unit="{token}",
                                         description="Tokens used per agent turn")

    answer = run_traced_turn(tracer, token_counter, "What bread do you have today?")
    print(f"\nAgent answer: {answer}\n")

    # Flush so spans and metrics are exported before the process exits.
    tracer_provider.force_flush()
    meter_provider.force_flush()
    print("Telemetry flushed. Look above for the exported spans (and metrics).")
