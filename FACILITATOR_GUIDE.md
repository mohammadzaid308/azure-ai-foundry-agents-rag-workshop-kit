# Facilitator Guide -- Azure AI Foundry Agents Workshop (7 hours, ~10 people)

> **For the presenter / instructor only.** Attendees see the lab READMEs and code.
> Print or keep this guide open on a second screen. Timings are targets, not hard limits.

---

## Pre-workshop checklist (day before)

- [ ] Provision Foundry projects for each attendee (or one shared project with individual Resource Group assignments).
- [ ] Each attendee has **Azure AI User** (or **Azure AI Project Manager**) role on the project - confirm in portal IAM.
- [ ] Deploy **gpt-4o** in the project's Model deployments.
- [ ] Clone the public repo URL and paste it in the invite:
      https://github.com/mohammadzaid308/azure-ai-foundry-agents-rag-workshop-kit
- [ ] Share the pre-read email (infrastructure prerequisites) in advance.
- [ ] Confirm Python 3.10+, Azure CLI, and git are installed on every machine.
- [ ] Have the Foundry portal (https://ai.azure.com) and Azure portal open in your browser.
- [ ] Do a **full dry run** of every lab the day before on the target subscription.
- [ ] Prepare one "golden run" .env file that works -- use it as a backup demo if someone's auth breaks.

---

## Room setup

| Item | Notes |
|------|-------|
| Screen mirroring | Share your VS Code and browser side by side |
| Breakout rooms (virtual) | One pair per lab for peer debugging |
| Chat/Slack channel | #workshop-help -- paste error messages here |
| Shared whiteboard | For group capstone sketch (Lab 7) |

---

## Agenda at a glance

| Block | Time | Content |
|-------|------|---------|
| 0 | 09:00-09:30 | Welcome + setup check |
| 1 | 09:30-10:15 | Lab 1 -- First model calls (Responses API + streaming) |
| 2 | 10:15-11:45 | Lab 2 -- Build, chat, tools, RAG |
| Break | 11:45-12:00 | |
| 3 | 12:00-12:45 | Lab 3 -- Grounding (pick 2 of 3) |
| 4 | 12:45-13:45 | Lab 4 -- Multi-agent orchestration |
| Lunch | 13:45-14:15 | |
| 5 | 14:15-15:00 | Lab 5 -- Evaluations + Security/Observability |
| 6 | 15:00-16:00 | Lab 6 -- MCP, Guardrails, Telemetry (pick 2 of 4) |
| 7 | 16:00-16:45 | Lab 7 -- Capstone sprint |
| 8 | 16:45-17:00 | Demo showcase + wrap-up |

---

## Block 0 -- Welcome + setup (09:00-09:30, 30 min)

### What to say
"Good morning everyone. Today is a code-first workshop -- I'll talk for about 5 minutes per
lab, then you code for the rest of the time. I'm here to unblock you, not lecture. By the end
of the day you'll have a real multi-agent system, an MCP server, guardrails, and a CI eval
gate -- all wired to Azure AI Foundry."

### Setup steps (walk attendees through together)

    git clone https://github.com/mohammadzaid308/azure-ai-foundry-agents-rag-workshop-kit
    cd azure-ai-foundry-agents-rag-workshop-kit/python
    python -m venv .venv && source .venv/bin/activate   # Windows: .venv\Scripts\activate
    pip install -r labs/01-responses/requirements.txt
    cp .env-template .env
    # Edit .env: fill in FOUNDRY_PROJECT_ENDPOINT and FOUNDRY_AGENT_NAME
    az login

Common blockers to watch for:
- az login on corporate laptop -> use --use-device-code flag
- Wrong subscription set -> az account set --subscription "<ID>"
- FOUNDRY_PROJECT_ENDPOINT must be:
  https://<resource>.services.ai.azure.com/api/projects/<project>
  (NOT just the resource URL)

### Portal orientation (2 min live demo on your screen)
"Let me quickly show you the Foundry portal. Three sections we visit today:
  1. Agents -- where your agents and conversations live
  2. Evaluation -- where your eval runs and scores appear
  3. Monitoring -> Traces -- where every API call is traced
Keep the portal open in a second tab all day. Every lab has a PORTAL comment
in the code telling you exactly what to look at after you run it."

---

## Block 1 -- Lab 1: First model calls (09:30-10:15, 45 min)

### What to say (5 min)
"Lab 1 is the simplest possible Foundry call. No agent, no tools -- just the Responses API,
which is OpenAI-compatible. Goal: confirm auth works and understand the client setup."

Labs: responses/quickstart-responses.py and streaming-responses/streaming-responses.py

Talking points while they code:
- AIProjectClient is the entry point -- wraps credentials + endpoint.
- get_openai_client() returns a standard OpenAI client but routed through your Foundry
  project for logging and billing.
- Point to portal: Foundry portal -> Monitoring -> Traces -- a new entry appears after the call.

### Challenge moment (~15 min before the block ends)
"Open the CHALLENGE comment at the bottom of quickstart-responses.py. Add a second call that
converts the answer to square km. Raise your hand when you have both numbers printing."

### Common questions

Q: What is the difference between responses.create and chat.completions.create?
A: Both work. responses.create returns .output_text directly and handles tool calls and
   conversations as a higher-level abstraction.

Q: Can I use my own OpenAI API key?
A: Yes, but you lose Foundry's audit logs, evaluations, and agent orchestration.

---

## Block 2 -- Lab 2: Build, chat, tools, RAG (10:15-11:45, 90 min)

### What to say (5 min)
"Lab 2 is the heart of the workshop. We create an agent -- a persistent, named model with
instructions stored server-side. Then we chat with it, add function tools that call local
Python, and add file-based RAG over a corpus of gift suggestions."

Pacing suggestion:
  create-agent      10 min  -- agents.create_version; agent lives in Foundry, not local code
  chat-with-agent   15 min  -- conversations.create + multi-turn thread
  agent-function    35 min  -- tool schema -> dispatch loop -> tool outputs
  filesystem-rag    30 min  -- upload corpus -> FileSearchTool -> citations

### Portal checkpoint -- STOP the room here at ~10:40
"Everyone stop for 60 seconds. Go to Foundry portal -> Agents. You should see your agent
listed with a version number. Click it -- you can see the instructions, the model, and every
conversation it has had. This is server-side state that persists even if you restart your script."

### Challenge moment (agent-function ~11:10)
"The CHALLENGE in agent-function asks you to add a get_order_status function. You have 10 min.
The trick: it goes into BOTH the tools list AND the dispatch loop. Who can answer
'What is in my order ORD-0001?' first?"

### Common questions

Q: The tool dispatch loop looks complex -- is there a simpler way?
A: Newer SDK versions support automatic function-calling. The manual loop teaches you
   what is happening underneath, which matters when things go wrong.

Q: Does FileSearchTool re-upload on every run?
A: Yes as coded here. In production you would store the vector_store_id and reuse it.

---

## Break (11:45-12:00, 15 min)

---

## Block 3 -- Lab 3: Grounding (12:00-12:45, 45 min)

### What to say (3 min)
"Grounding stops the model from hallucinating by tethering it to real data at query time.
With Bing it is live web results; with AI Search it is your own indexed documents;
with OpenAPI it calls any REST API as a tool. Pick two of the three labs."

Recommended fallback if connections are missing: openapi-tool (no connection ID needed)

Pacing:
  bing-grounding       20 min  (needs Bing connection ID)
  azure-ai-search      20 min  (needs search index + connection)
  openapi-tool         20 min  (no connection needed -- RECOMMENDED fallback)

### Portal checkpoint
"After bing-grounding: Foundry portal -> Agents -> <agent> -> Playground. Ask the same
question and click 'Show details' to see which URLs were cited."

### Challenge (openapi-tool)
"The OpenAPI challenge asks you to add a second tool (dog breeds API). Goal: understand that
any REST API becomes an agent tool with a 20-line OpenAPI spec."

---

## Block 4 -- Lab 4: Multi-agent (12:45-13:45, 60 min)

### What to say (5 min)
"Lab 4 wires together multiple agents. Sequential: each agent hands off to the next
(intake -> specialist -> synthesizer). Concurrent: asyncio.gather fans out and a final
agent aggregates. This is the core pattern of real agentic pipelines."

### Demo first (3 min live on your screen)
Run multi-agent-sequential.py before attendees start. Show the output so they know
what success looks like. Note: this takes ~90 seconds (three model calls in sequence).
Warn attendees so they do not think it froze.

### Portal checkpoint -- STOP the room at ~13:10
"Open Foundry portal -> Agents -> Workflows. Click 'View diagram'. You should see the
three-step DAG rendered visually. In the Playground you can run the workflow interactively
and see each agent's contribution highlighted."

### Challenge
"Lab 4a CHALLENGE: add a quality-check agent as the fourth step. 10 minutes. The first
team that gets the WARNING to appear for a nut-containing cake question wins."

---

## Lunch (13:45-14:15, 30 min)

Suggest attendees browse the conversation history from Lab 4 in the portal while eating.

---

## Block 5 -- Lab 5: Evaluations + Observability (14:15-15:00, 45 min)

### What to say (5 min)
"Lab 5 is about knowing whether your agent is good, and knowing when it breaks.
Evaluations run graded benchmark suites and surface scores in the portal.
Observability gives you traces you can alert on."

Pacing:
  evaluations (--scenario dataset)   20 min
  security-observability             15 min
  Discussion: what thresholds set?   10 min

### Portal checkpoint -- BIG pause, stop the room at ~14:35
"Everyone go to Foundry portal -> Evaluation -> Evaluation runs. Find your run and click it.
Walk me through what you see. Who has the highest groundedness score? Why might two people
have different scores for the same dataset? What threshold would you set for a production
release gate?"

### Challenge
"The evaluations CHALLENGE: add a custom tone evaluator that scores friendliness. The portal
should show it as a new column in the results table. 15 minutes."

---

## Block 6 -- Lab 6: MCP, Guardrails, Telemetry (15:00-16:00, 60 min)

Attendees choose 2 of the 4 Lab 6 add-ons.
Most impactful pairing: guardrails + telemetry (fully offline, no extra setup).
For devs who want to go deeper: mcp-server + evaluations-tests.

### What to say (5 min)
"Lab 6 is the engineering excellence layer -- the stuff that takes an agent from demo to
production. All four run with no Azure calls so you see results immediately."

  mcp-server            Best for: backend devs who want to expose their own API as a tool
  evaluations-tests     Best for: DevOps / CI engineers who want eval as a PR gate
  guardrails            Best for: security and compliance teams
  telemetry             Best for: ops / SRE / anyone who has been paged at 2am

### Portal checkpoint (guardrails -- no portal, worth the discussion)
"The guardrails demo blocked three of four inputs. Write one prompt you think would slip past
the current rules. Add it to adversarial_cases.jsonl. Add the missing detection to
evaluators.py and make pytest pass. This is what red-teaming looks like."

### Challenge
"The telemetry CHALLENGE: add a latency histogram metric. Simulate a slow turn with
time.sleep(0.5). BONUS: write a KQL query in App Insights that shows P95 latency."

---

## Block 7 -- Lab 7: Capstone sprint (16:00-16:45, 45 min)

### What to say (5 min)
"You have 40 minutes to build something. Open capstone/README.md -- eight ideas inside.
Pick one or invent your own. Work solo or in pairs. At 16:45 everyone does a 2-minute demo:
scenario, what you built, one thing that surprised you."

Suggested pairings by interest:
  Full product   -> Idea 1: Frankie's Bakery concierge (MCP + guardrails + telemetry + eval)
  Security       -> Idea 3: Red-team your agent
  Architecture   -> Idea 4: MCP marketplace (two servers + per-tool approval flow)
  Ops            -> Idea 5: Observability dashboard with KQL workbook
  Creative       -> Idea 2: GiftBot multi-agent gift planner

Your role during the sprint:
- Walk the room every 8 minutes.
- If someone is stuck on setup, redirect to the closest working lab and extend from there.
- Prompt pairs to sketch their architecture before coding.
- Resist the urge to live-code for attendees. Ask: "What tools would this agent need?
  What would the instructions say? How would you test it?"

---

## Block 8 -- Demo showcase + wrap-up (16:45-17:00, 15 min)

Demo format -- 2 minutes per person/pair:
  1. What question did you ask?
  2. What did the agent do?
  3. One surprising thing you learned.

### Closing remarks to say
"You have gone from a single responses.create call to a production-grade agent with tools,
grounding, multi-agent orchestration, an MCP server, guardrails, OpenTelemetry tracing, and
automated eval gates. The same pattern scales: swap the bakery data for your domain data,
connect your internal APIs as MCP tools, and you have a production system.

The repo stays public -- use it, fork it, contribute back.
The PORTAL and CHALLENGE comments are there if you want to go deeper on any lab tonight."

Share with attendees before they leave:
  Public repo:     https://github.com/mohammadzaid308/azure-ai-foundry-agents-rag-workshop-kit
  Foundry docs:    https://learn.microsoft.com/azure/ai-foundry/
  Foundry samples: https://github.com/azure/azure-ai-foundry-samples

---

## Troubleshooting quick reference

| Error | Likely cause | Fix |
|-------|-------------|-----|
| 401 Unauthorized | az login expired | az login then re-run |
| 403 Forbidden | Missing RBAC role | Add Azure AI User role in project IAM |
| FOUNDRY_PROJECT_ENDPOINT not found | .env not loaded | source .env or check load_dotenv() path |
| ModuleNotFoundError | venv not active | source .venv/bin/activate |
| AgentNotFound | Wrong FOUNDRY_AGENT_NAME | Check portal -> Agents for exact name |
| Lab 4 allow_preview error | Preview flag required | AIProjectClient(..., allow_preview=True) |
| Lab 6 MCP connection refused | Server not running | python bakery_mcp_server.py --http in another terminal |
| Lab 6 MCP tunnel unreachable | Public URL needed | devtunnel host -p 8000 or ngrok http 8000 |

---

## Key numbers to keep in mind

- Average lab run time (excluding model calls): < 5 seconds
- Lab 1 model call latency: ~2-4 seconds
- Lab 4 sequential (3 agents): ~60-90 seconds
- Evaluation dataset run: ~2-5 minutes (depends on dataset size)
- Lab 6 offline labs (guardrails, telemetry): < 2 seconds each
- Total model calls across all labs: ~30-50 (fits well within standard quota)
