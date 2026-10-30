using System;

namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	/// <summary>
	/// The Last Bridge (stage_11), the castle's first chapter: .ai/achievements-and-secrets.md 8.12 and
	/// .ai/endgame-castle.md. Past the gate, so its rewards may be larger - still never XP, levels, tags,
	/// charges or Growth in a run.
	/// </summary>
	/// <remarks>
	/// To build: the Toll Stone (touch), the Span Lantern (linger), the Gargoyle's Perch (strike, on
	/// Breakable) and the Names on the Parapet (the keepsake - gather, on SecretGather).
	/// </remarks>
	private static class Bridge
	{
		internal static SecretDefinition[] Build() => Array.Empty<SecretDefinition>();
	}
}
