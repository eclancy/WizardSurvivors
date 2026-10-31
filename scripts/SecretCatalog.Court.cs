using System;

namespace WizardSurvivors.scripts;

public static partial class SecretCatalog
{
	/// <summary>
	/// The Court (stage_12): .ai/achievements-and-secrets.md 8.12 and .ai/endgame-castle.md. Its rooms are
	/// its secrets.
	/// </summary>
	/// <remarks>
	/// To build: the Great Hall, the Chapel, the Library (reveals every secret's hint, 4.3), the Armoury,
	/// the Map Room, the Treasury, the Gallery, the Kennels, the Kitchens, the Minstrels' Gallery, the
	/// Solar, and the Keepsake Doors (one door for every chapter keepsake found).
	/// </remarks>
	private static class Court
	{
		internal static SecretDefinition[] Build() => Array.Empty<SecretDefinition>();
	}
}
