import asyncio
import os

from dotenv import load_dotenv
from azure.identity.aio import DefaultAzureCredential
from azure.ai.projects.aio import AIProjectClient

load_dotenv()
PROJECT_ENDPOINT = os.environ["FOUNDRY_PROJECT_ENDPOINT"]
MODEL_DEPLOYMENT = os.environ.get("FOUNDRY_MODEL_DEPLOYMENT", "gpt-4o")

PRODUCT = "a smart water bottle"


async def main():
    async with DefaultAzureCredential() as credential:
        async with AIProjectClient(endpoint=PROJECT_ENDPOINT, credential=credential) as project:
            openai = project.get_openai_client()

            async def ask(prompt: str) -> str:
                resp = await openai.responses.create(model=MODEL_DEPLOYMENT, input=prompt)
                return resp.output_text

            # Concurrent pattern: independent agents run in parallel, then aggregate.
            pros, cons = await asyncio.gather(
                ask(f"You are an optimist. List 3 pros of {PRODUCT}."),
                ask(f"You are a skeptic. List 3 cons of {PRODUCT}."),
            )
            print("--- Pros ---\n" + pros)
            print("\n--- Cons ---\n" + cons)

            verdict = await ask(
                "You are a product analyst. Give a one-sentence verdict.\n\n"
                f"PROS:\n{pros}\n\nCONS:\n{cons}"
            )
            print("\n--- Verdict ---\n" + verdict)


if __name__ == "__main__":
    asyncio.run(main())
