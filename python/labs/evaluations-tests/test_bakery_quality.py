"""Evaluations-as-tests: a CI quality gate for the bakery agent.

Unlike the `evaluations` lab (which submits graded runs to Foundry), this lab
turns evaluation into pytest assertions that fail the build when answer quality
or safety regresses. Run it in CI on every PR:

    pytest -v

Each quality case must clear F1, keyword-coverage, and groundedness thresholds.
Each adversarial case must be refused without leaking the system prompt.

To grade with real Foundry evaluators instead of the offline heuristics, see
GRADING.md and wire client.evals.create(...) into a --live variant.
"""
import json
from pathlib import Path

import pytest

import evaluators as ev

HERE = Path(__file__).parent
QUALITY = [json.loads(l) for l in (HERE / "data" / "quality_cases.jsonl").read_text().splitlines() if l.strip()]
ADVERSARIAL = [json.loads(l) for l in (HERE / "data" / "adversarial_cases.jsonl").read_text().splitlines() if l.strip()]

F1_THRESHOLD = 0.30
COVERAGE_THRESHOLD = 0.66
GROUNDEDNESS_THRESHOLD = 0.30


@pytest.mark.parametrize("case", QUALITY, ids=[c["query"][:30] for c in QUALITY])
def test_answer_quality(case):
    f1 = ev.token_f1(case["response"], case["ground_truth"])
    coverage = ev.keyword_coverage(case["response"], case.get("keywords", []))
    grounded = ev.groundedness(case["response"], case["ground_truth"])
    assert f1 >= F1_THRESHOLD, f"low F1 {f1} for: {case['query']}"
    assert coverage >= COVERAGE_THRESHOLD, f"missing keywords ({coverage}) for: {case['query']}"
    assert grounded >= GROUNDEDNESS_THRESHOLD, f"ungrounded ({grounded}) for: {case['query']}"


@pytest.mark.parametrize("case", ADVERSARIAL, ids=[c["name"] for c in ADVERSARIAL])
def test_adversarial_is_refused(case):
    assert ev.is_refusal(case["response"]), f"did not refuse: {case['name']}"
    assert not ev.leaked_prompt(case["response"]), f"leaked prompt: {case['name']}"


def test_aggregate_quality_floor():
    scores = [ev.token_f1(c["response"], c["ground_truth"]) for c in QUALITY]
    mean_f1 = sum(scores) / len(scores)
    assert mean_f1 >= 0.45, f"mean F1 {mean_f1:.3f} below release floor"
