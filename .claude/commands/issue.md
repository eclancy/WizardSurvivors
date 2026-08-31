---
description: Triage a GitHub issue into an implementation plan
argument-hint: <issue number or search text>
allowed-tools: Read, Grep, Glob, Bash(gh issue:*), Bash(git log:*), Bash(git diff:*)
---
Triage the issue matching "$ARGUMENTS".

If it is a number, `gh issue view <n>`. Otherwise `gh issue list --search "$ARGUMENTS"`
and, if several match, list them and ask which one.

Then, **without writing any code**:

1. Summarize what the issue actually asks for.
2. Map it onto real files — which of `scripts/*.cs`, which `scenes/*.tscn`, which
   root `.tres` resources would change. Cite `file:line`.
3. **Check whether it is already done.** Several issues predate work that has
   since landed, and the chest-item and enemy-spacing issues in particular
   describe systems that already exist. Read the code and `git log` before
   assuming anything is open.
4. Check whether the art it needs is already in the repo. `assets/organized/`
   holds ~1600 imported PNGs and several issues are blocked only on *wiring*
   art, not acquiring it. Search before recommending new assets.
5. Flag these impacts specifically, because they get missed: collision
   layer/mask propagation, the `ContentValidator.SpellResourcePaths` allowlist,
   and `SaveData` migration for existing saves.
6. Give an ordered plan with the smallest shippable first slice.

GitHub is the single backlog — `.ai/issues/` no longer exists. `.ai/` still holds
the design docs, and `.ai/archive/` holds historical status reports that are NOT
current.
