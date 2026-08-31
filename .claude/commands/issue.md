---
description: Triage a .ai/issues/ backlog spec into an implementation plan
argument-hint: <issue filename fragment>
allowed-tools: Read, Grep, Glob, Bash(git log:*), Bash(git diff:*)
---
Find the spec in `.ai/issues/` matching "$1". There are ~13; if several match,
list them and ask which one.

Then, **without writing any code**:

1. Summarize what the spec actually asks for.
2. Map it onto real files — which of `scripts/*.cs`, which `scenes/*.tscn`,
   which root `.tres` resources would change. Cite `file:line`.
3. Flag anything already implemented. Several of these specs predate work that
   has since landed — check the code and `git log` before assuming it is open.
4. Flag these impacts specifically, because they are the ones that get missed:
   collision layer/mask propagation, the `ContentValidator.SpellResourcePaths`
   allowlist, and `SaveData` migration for existing save files.
5. Give an ordered plan with the smallest shippable first slice.
