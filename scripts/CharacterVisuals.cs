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

		Texture2D frameTwo = ResolveSecondIdleFrame(frameOne) ?? frameOne;

		frames = new SpriteFrames();
		frames.AddAnimation("idle");
		frames.SetAnimationLoop("idle", true);
		frames.SetAnimationSpeed("idle", 4.0f);
		frames.AddFrame("idle", frameOne);
		frames.AddFrame("idle", frameTwo);
		return true;
	}

	private static Texture2D ResolveSecondIdleFrame(Texture2D frameOne)
	{
		string path = frameOne.ResourcePath;
		if (string.IsNullOrWhiteSpace(path))
			return null;

		int suffixIndex = path.LastIndexOf("_1.");
		if (suffixIndex < 0)
			return null;

		string frameTwoPath = path.Substring(0, suffixIndex) + "_2." + path.Substring(suffixIndex + 3);
		if (!ResourceLoader.Exists(frameTwoPath))
			return null;

		return ResourceLoader.Load<Texture2D>(frameTwoPath);
	}
}