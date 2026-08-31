---
description: Write an architecture decision record into .ai/decisions/
argument-hint: <decision title>
allowed-tools: Read, Glob, Write
---
Write an ADR for: $ARGUMENTS

Read both existing records in `.ai/decisions/` first and match their format
exactly. Filename: `.ai/decisions/YYYY-MM-DD-<kebab-title>.md`.

Use today's real date. Note that the two existing records use a literal
`2025-01-xx` placeholder — do not copy that placeholder forward.

Cover: context, the decision, alternatives considered and why they were
rejected, and consequences. Cite the specific files and systems affected.
Keep it under one page.
