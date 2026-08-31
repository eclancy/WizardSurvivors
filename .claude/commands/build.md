---
description: Compile-check the C# project with the dotnet CLI
allowed-tools: Bash(dotnet build:*), Read, Grep, Glob
---
!`dotnet build WizardSurvivors.sln -v q --nologo`

Summarize the build output above. For each error or warning, name the file and
line and the smallest fix that would resolve it. Do not apply any fix unless I
asked you to.

Remember: a clean build does NOT validate `.tscn` wiring, resource paths, or
signal connections. Only running Godot does that. If I am relying on this
command to confirm a change works, say so explicitly.
