using System;

namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	/// <summary>
	/// The Emberdeep (stage_7), the gate chapter: .ai/achievements-and-secrets.md 8.10. No side event, and
	/// no vault: whatever the gate counts, a secret here pays nothing it counts.
	/// </summary>
	/// <remarks>
	/// To build: E1 the Last Ward (the anchor), E2 the Sleeping Sentry, E3 the Marks (the keepsake, page
	/// only) and E4 the Quench Trough. He is never on screen here (tone rule 5).
	/// </remarks>
	private static class Emberdeep
	{
		internal static SecretDefinition[] Build() => Array.Empty<SecretDefinition>();
	}
}
