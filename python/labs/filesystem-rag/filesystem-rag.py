import os
import re
from pathlib import Path

from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient


def tokenize(text: str) -> set[str]:
    return set(re.findall(r"[a-zA-Z0-9]+", text.lower()))


def retrieve_context(data_dir: Path, question: str, top_k: int = 2) -> str:
    question_tokens = tokenize(question)
    scored: list[tuple[int, str, str]] = []

    for file in data_dir.glob("*.md"):
        content = file.read_text(encoding="utf-8")
        score = len(question_tokens.intersection(tokenize(content)))
        scored.append((score, file.name, content))

    scored.sort(key=lambda item: item[0], reverse=True)
    top_docs = scored[:top_k]

    chunks: list[str] = []
    for score, name, content in top_docs:
        chunks.append(f"[{name}] (score={score})\n{content}")
    return "\n\n".join(chunks)


def main() -> None:
    load_dotenv()
    endpoint = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
    model = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4.1-mini")

    project = AIProjectClient(endpoint=endpoint, credential=DefaultAzureCredential())
    openai = project.get_openai_client()

    question = "What is our policy for travel expense approval and receipts?"
    data_dir = Path(__file__).parent / "data"
    context = retrieve_context(data_dir, question)

    prompt = (
        "You are an assistant that must answer using only the provided context.\n"
        "If the answer is not in context, say you do not know.\n\n"
        f"Context:\n{context}\n\nQuestion: {question}"
    )

    response = openai.responses.create(model=model, input=prompt)
    print(response.output_text)


if __name__ == "__main__":
    main()

