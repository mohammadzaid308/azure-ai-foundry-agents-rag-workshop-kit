"""Lab: Foundry Evaluations (Frankie's Bakery quality gates).

Four SDK evaluation scenarios against Azure AI Foundry:
  --scenario dataset : grade pre-computed answers from data/bakery_eval_dataset.jsonl
  --scenario model   : send live queries to a model and grade the answers
  --scenario agent   : send live queries to a deployed agent and grade the answers
  --scenario traces  : grade agent traces captured in Application Insights

Usage:
    python evaluations.py --scenario dataset

Env vars (kit convention):
    FOUNDRY_PROJECT_ENDPOINT        required
    FOUNDRY_MODEL_DEPLOYMENT        target + judge model (default gpt-4o)
    FOUNDRY_AGENT_NAME              for the agent scenario
    FOUNDRY_AGENT_ID                e.g. bakery-agent:1 (for the traces scenario)
"""
import argparse
import os
import time
from pathlib import Path

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient

load_dotenv()
ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
MODEL_DEPLOYMENT = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4o")
AGENT_NAME = os.environ.get("FOUNDRY_AGENT_NAME", "frankies-bakery-support")
AGENT_ID = os.environ.get("FOUNDRY_AGENT_ID", "bakery-agent:1")

project_client = AIProjectClient(endpoint=ENDPOINT, credential=DefaultAzureCredential())
client = project_client.get_openai_client()

HERE = Path(__file__).parent
DATASET_PATH = str(HERE / "data" / "bakery_eval_dataset.jsonl")
QUERIES_PATH = str(HERE / "data" / "bakery_queries_only.jsonl")


def poll_until_done(eval_object, eval_run):
    print(f"Run started: {eval_run.id}")
    while True:
        run = client.evals.runs.retrieve(run_id=eval_run.id, eval_id=eval_object.id)
        print(f"  Status: {run.status}")
        if run.status in ("completed", "failed"):
            break
        time.sleep(5)
    print(f"Final status: {run.status}")
    print(f"Report URL:   {run.report_url}")
    return run


def print_results(eval_object, eval_run):
    items = list(client.evals.runs.output_items.list(run_id=eval_run.id, eval_id=eval_object.id))
    print(f"\n{'-' * 60}\nResults ({len(items)} rows):")
    for item in items:
        query = item.datasource_item.get("query", "")
        if isinstance(query, list):
            query = next((m["content"] for m in query if m.get("role") == "user"), str(query))
        print(f"\n  Query: {str(query)[:80]}")
        for result in item.results:
            print(f"    [{getattr(result, 'name', '?')}] "
                  f"{getattr(result, 'label', '?')} (score={getattr(result, 'score', 'N/A')})  "
                  f"{(getattr(result, 'reason', '') or '')[:100]}")


def run_dataset_scenario():
    print("\n=== SCENARIO: Dataset Evaluation ===")
    data_id = project_client.datasets.upload_file(
        name="bakery-precomputed", version="1", file_path=DATASET_PATH).id
    print(f"Dataset uploaded: {data_id}")

    eval_object = client.evals.create(
        name="Bakery Dataset Evaluation",
        data_source_config={
            "type": "custom",
            "item_schema": {
                "type": "object",
                "properties": {"query": {"type": "string"}, "response": {"type": "string"},
                               "ground_truth": {"type": "string"}},
                "required": ["query", "response", "ground_truth"],
            },
        },
        testing_criteria=[
            {"type": "azure_ai_evaluator", "name": "coherence",
             "evaluator_name": "builtin.coherence",
             "initialization_parameters": {"deployment_name": MODEL_DEPLOYMENT},
             "data_mapping": {"query": "{{item.query}}", "response": "{{item.response}}"}},
            {"type": "azure_ai_evaluator", "name": "f1",
             "evaluator_name": "builtin.f1_score",
             "data_mapping": {"response": "{{item.response}}", "ground_truth": "{{item.ground_truth}}"}},
            {"type": "azure_ai_evaluator", "name": "violence",
             "evaluator_name": "builtin.violence",
             "data_mapping": {"query": "{{item.query}}", "response": "{{item.response}}"}},
        ],
    )
    eval_run = client.evals.runs.create(
        eval_id=eval_object.id, name="bakery-dataset-sdk-run",
        data_source={"type": "jsonl", "source": {"type": "file_id", "id": data_id}})
    run = poll_until_done(eval_object, eval_run)
    if run.status == "completed":
        print_results(eval_object, eval_run)


def run_model_scenario():
    print("\n=== SCENARIO: Model Target Evaluation ===")
    data_id = project_client.datasets.upload_file(
        name="bakery-queries", version="1", file_path=QUERIES_PATH).id
    print(f"Dataset uploaded: {data_id}")

    eval_object = client.evals.create(
        name="Bakery Model Target Evaluation",
        data_source_config={
            "type": "custom",
            "item_schema": {"type": "object", "properties": {"query": {"type": "string"}},
                            "required": ["query"]},
            "include_sample_schema": True,
        },
        testing_criteria=[
            {"type": "azure_ai_evaluator", "name": "coherence",
             "evaluator_name": "builtin.coherence",
             "initialization_parameters": {"deployment_name": MODEL_DEPLOYMENT},
             "data_mapping": {"query": "{{item.query}}", "response": "{{sample.output_text}}"}},
            {"type": "azure_ai_evaluator", "name": "violence",
             "evaluator_name": "builtin.violence",
             "data_mapping": {"query": "{{item.query}}", "response": "{{sample.output_text}}"}},
        ],
    )
    eval_run = client.evals.runs.create(
        eval_id=eval_object.id, name="bakery-model-sdk-run",
        data_source={
            "type": "azure_ai_target_completions",
            "source": {"type": "file_id", "id": data_id},
            "input_messages": {
                "type": "template",
                "template": [
                    {"type": "message", "role": "system",
                     "content": {"type": "input_text",
                                 "text": "You are the Frankie's Bakery Customer Support Agent. "
                                         "Answer questions about products, store hours, and policies."}},
                    {"type": "message", "role": "user",
                     "content": {"type": "input_text", "text": "{{item.query}}"}},
                ],
            },
            "target": {"type": "azure_ai_model", "model": MODEL_DEPLOYMENT,
                       "sampling_params": {"max_completion_tokens": 512}},
        })
    run = poll_until_done(eval_object, eval_run)
    if run.status == "completed":
        print_results(eval_object, eval_run)


def run_agent_scenario():
    print(f"\n=== SCENARIO: Agent Target Evaluation (agent={AGENT_NAME}) ===")
    data_id = project_client.datasets.upload_file(
        name="bakery-agent-queries", version="1", file_path=QUERIES_PATH).id
    print(f"Dataset uploaded: {data_id}")

    eval_object = client.evals.create(
        name="Bakery Agent Target Evaluation",
        data_source_config={
            "type": "custom",
            "item_schema": {"type": "object", "properties": {"query": {"type": "string"}},
                            "required": ["query"]},
            "include_sample_schema": True,
        },
        testing_criteria=[
            {"type": "azure_ai_evaluator", "name": "task_adherence",
             "evaluator_name": "builtin.task_adherence",
             "initialization_parameters": {"deployment_name": MODEL_DEPLOYMENT},
             "data_mapping": {"query": "{{item.query}}", "response": "{{sample.output_items}}"}},
            {"type": "azure_ai_evaluator", "name": "coherence",
             "evaluator_name": "builtin.coherence",
             "initialization_parameters": {"deployment_name": MODEL_DEPLOYMENT},
             "data_mapping": {"query": "{{item.query}}", "response": "{{sample.output_text}}"}},
        ],
    )
    eval_run = client.evals.runs.create(
        eval_id=eval_object.id, name="bakery-agent-sdk-run",
        data_source={
            "type": "azure_ai_target_completions",
            "source": {"type": "file_id", "id": data_id},
            "input_messages": {
                "type": "template",
                "template": [{"type": "message", "role": "user",
                              "content": {"type": "input_text", "text": "{{item.query}}"}}],
            },
            "target": {"type": "azure_ai_agent", "name": AGENT_NAME},
        })
    run = poll_until_done(eval_object, eval_run)
    if run.status == "completed":
        print_results(eval_object, eval_run)


def run_traces_scenario():
    print(f"\n=== SCENARIO: Trace Evaluation (agent_id={AGENT_ID}) ===")
    eval_object = client.evals.create(
        name="Bakery Trace Evaluation",
        data_source_config={"type": "azure_ai_source", "scenario": "traces"},
        testing_criteria=[
            {"type": "azure_ai_evaluator", "name": "intent_resolution",
             "evaluator_name": "builtin.intent_resolution",
             "initialization_parameters": {"deployment_name": MODEL_DEPLOYMENT},
             "data_mapping": {"query": "{{item.query}}", "response": "{{item.response}}"}},
            {"type": "azure_ai_evaluator", "name": "violence",
             "evaluator_name": "builtin.violence",
             "data_mapping": {"query": "{{item.query}}", "response": "{{item.response}}"}},
        ],
    )
    eval_run = client.evals.runs.create(
        eval_id=eval_object.id, name="bakery-trace-sdk-run",
        data_source={"type": "azure_ai_traces", "agent_id": AGENT_ID,
                     "max_traces": 50, "lookback_hours": 1})
    run = poll_until_done(eval_object, eval_run)
    if run.status == "completed":
        print_results(eval_object, eval_run)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Foundry Evaluations scenarios")
    parser.add_argument("--scenario", choices=["dataset", "model", "agent", "traces"],
                        default="dataset", help="Which evaluation scenario to run")
    args = parser.parse_args()
    {"dataset": run_dataset_scenario, "model": run_model_scenario,
     "agent": run_agent_scenario, "traces": run_traces_scenario}[args.scenario]()
