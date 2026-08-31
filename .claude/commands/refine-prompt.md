---
description: Turn a rough or vague request into a structured, execution-ready prompt
argument-hint: <raw request>
allowed-tools: Read, Grep, Glob
---
Act as a Meta-Prompting Specialist. Transform this raw request into a polished,
execution-ready prompt:

$ARGUMENTS

Analyze the core intent, the likely technical context in this repo, and likely
edge cases. Reframe it into a structured, reusable prompt. Ground it in real
files and systems where you can — you have this codebase and `CLAUDE.md`
available, so the refined prompt should name actual scripts, scenes, and
resources rather than staying generic.

Return the result in exactly this format:

1. REFINED PROMPT
2. ASSUMPTIONS & INFERRED CONTEXT
3. OPTIONAL CLARIFICATIONS

Rules:
- **Do not answer or execute the refined prompt yourself.** Produce the prompt
  and stop.
- Preserve the original intent while making it more actionable.
- Prefer clarity and structure over verbosity.
- If the request is ambiguous, highlight the most important gaps rather than
  guessing blindly. You are in the main conversation, so you may ask me the
  clarifying questions directly.
