using System;

namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	/// <summary>
	/// The Sketchbook (stage_8): .ai/stage-secrets.md 6.9, unchanged by .ai/achievements-and-secrets.md
	/// 8.11. Outside the campaign and the fiction: page marks only, and the kids' art is never altered,
	/// extended or given derived frames - ValidateSecretCatalog holds the first rule, CLAUDE.md the second.
	/// </summary>
	/// <remarks>
	/// To build: K1 the Dog's Find (follow the dog), K2 Colour It In (circle). K3 the Kids' Page is a
	/// reserved slot for something the kids draw, if and when they want to.
	/// </remarks>
	private static class Sketchbook
	{
		internal static SecretDefinition[] Build() => Array.Empty<SecretDefinition>();
	}
}
