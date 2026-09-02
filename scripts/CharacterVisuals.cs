using Godot;

namespace WizardSurvivors.scripts;

// Shared character look rules so CharacterSelection and Player stay in sync.
public static class CharacterVisuals
{
	public static Color GetCharacterTint(string characterId)
	{
		switch ((characterId ?? string.Empty).Trim().ToLowerInvariant())
		{
				case "test_wizard":
					return new Color(0.78f, 1.0f, 0.78f);
			case "pyromancer":
				return new Color(1.0f, 0.78f, 0.64f);
			case "frostweaver":
				return new Color(0.72f, 0.90f, 1.0f);
			case "apprentice_wizard":
			default:
				return new Color(0.95f, 0.96f, 1.0f);
		}
	}

	public static bool TryBuildIdleFrames(Texture2D frameOne, out SpriteFrames frames)
	{
		frames = null;
		if (frameOne == null)
			return false;

		frames = new SpriteFrames();
		frames.AddAnimation("idle");
		frames.SetAnimationLoop("idle", true);
		frames.SetAnimationSpeed("idle", 6.0f);
		frames.AddFrame("idle", frameOne);

		// The character sheets ship a four-frame idle. Walk the siblings until one is missing, so
		// a portrait with fewer frames still yields a valid animation.
		for (int frameNumber = 2; frameNumber <= MaxIdleFrames; frameNumber++)
		{
			Texture2D next = ResolveIdleFrame(frameOne, frameNumber);
			if (next == null)
				break;
			frames.AddFrame("idle", next);
		}

		return true;
	}

	private const int MaxIdleFrames = 4;
	private static readonly string[] IdleFrameSeparators = { "-", "_" };

	// Finds the sibling frame "<portrait><sep><n>.png" next to frame one.
	//
	// This used to probe only for "_1.", the naming the raw asset packs use. The organized asset
	// tree renames those to "-1.png", so the probe silently stopped matching, the caller fell back
	// to duplicating frame one, and every character has been idling as a static sprite ever since.
	// Both separators are accepted now so either naming resolves.
	private static Texture2D ResolveIdleFrame(Texture2D frameOne, int frameNumber)
	{
		string path = frameOne.ResourcePath;
		if (string.IsNullOrWhiteSpace(path))
			return null;

		foreach (string separator in IdleFrameSeparators)
		{
			string token = separator + "1.";
			int suffixIndex = path.LastIndexOf(token, System.StringComparison.Ordinal);
			if (suffixIndex < 0)
				continue;

			string candidate = path.Substring(0, suffixIndex)
				+ separator + frameNumber + "."
				+ path.Substring(suffixIndex + token.Length);
			if (ResourceLoader.Exists(candidate))
				return ResourceLoader.Load<Texture2D>(candidate);
		}

		return null;
	}
}