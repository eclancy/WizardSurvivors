using Godot;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Shared character look rules so CharacterSelection and Player stay in sync.
public static class CharacterVisuals
{
	/// <summary>
	/// Where each wizard's staff head sits in their 48x48 cell, in pixels, or null for a character
	/// without one. Measured from the generated art (the centroid of the staff head's emissive
	/// pixels in frame 1). They are numbers, not a link: redraw a wizard in tools/art/roster.py and
	/// these have to be measured again.
	/// </summary>
	/// <remarks>
	/// Read by Player.StaffTipLocal, which is where a held spell comes out: Cinderbreath is a jet
	/// poured from the staff, and with the wizards no longer all holding their staff at the same
	/// spot, firing it from the body's centre put the Pyromancer's flames in his chest while his
	/// brazier was held out in front of him. The Geomancer's is the glowing pixel in his spike.
	/// </remarks>
	/// <summary>
	/// True for a wizard whose sheet is drawn facing LEFT. Player flips the body sprite whenever the
	/// player moves left, which assumes the art faces right; the Stormcaller is drawn mid-stride to
	/// the left - cloak streaming out behind him to the right - so he walked backwards in every
	/// direction. The others are drawn facing right or the viewer. If a wizard is redrawn in
	/// tools/art/roster.py, check which way the walk cycle strides and keep this in step.
	/// </summary>
	public static bool SpriteFacesLeft(string characterId)
	{
		return (characterId ?? string.Empty).Trim().ToLowerInvariant() == "stormcaller";
	}

	public static Vector2? GetStaffTipCell(string characterId)
	{
		switch ((characterId ?? string.Empty).Trim().ToLowerInvariant())
		{
			case "pyromancer": return new Vector2(40, 8);
			case "frostweaver": return new Vector2(36, 8);
			case "stormcaller": return new Vector2(11, 3);
			case "geomancer": return new Vector2(37, 19);
			case "tituba": return new Vector2(9, 7);
			case "vainamoinen": return new Vector2(16, 7);
			// The centre of his scrying mirror (tools/art/roster.py, dee(): MX, MY), not the motes in
			// his cupped hand, which are the same ramp.
			case "john_dee": return new Vector2(33, 7);
			default: return null;
		}
	}

	public static Color GetCharacterTint(string characterId)
	{
		switch ((characterId ?? string.Empty).Trim().ToLowerInvariant())
		{
			// EVERY CHARACTER WITH ART OF ITS OWN RENDERS UNTINTED. The tints below existed because
			// the whole roster shared one wizard sheet and hue was the only thing telling them
			// apart - which art-direction.md lists as a Don't, and which was always a stopgap
			// waiting for real sprites (#30). Those sprites exist now: each starter is drawn from
			// the spell it casts, in its own saturated pigment row, so a multiply over it is not
			// identity, it is damage. It drags a true red toward brown and a true blue toward grey,
			// and it turns a lit face sickly.
			case "test_wizard":
			case "pyromancer":
			case "frostweaver":
			case "stormcaller":
			case "geomancer":
			case "tituba":
			case "vainamoinen":
			case "john_dee":
			case "laelaps":
				return Colors.White;
			case "apprentice_wizard":
			default:
				return new Color(0.95f, 0.96f, 1.0f);
		}
	}

	/// <summary>How fast every wizard's idle plays, in the run and on the character select alike.</summary>
	public const float IdleFramesPerSecond = 6.0f;

	public static bool TryBuildIdleFrames(Texture2D frameOne, out SpriteFrames frames)
	{
		frames = null;
		List<Texture2D> idle = GetIdleFrames(frameOne);
		if (idle.Count == 0)
			return false;

		frames = new SpriteFrames();
		frames.AddAnimation("idle");
		frames.SetAnimationLoop("idle", true);
		frames.SetAnimationSpeed("idle", IdleFramesPerSecond);
		foreach (Texture2D frame in idle)
			frames.AddFrame("idle", frame);
		return true;
	}

	/// <summary>
	/// Frame one and the numbered siblings after it, as a plain list - for a menu that draws the idle
	/// itself (PixelArtRect) rather than through an AnimatedSprite2D. Empty when there is no frame one.
	/// </summary>
	public static List<Texture2D> GetIdleFrames(Texture2D frameOne)
	{
		var frames = new List<Texture2D>();
		if (frameOne == null)
			return frames;

		frames.Add(frameOne);
		// The character sheets ship a four-frame idle. Walk the siblings until one is missing, so
		// a portrait with fewer frames still yields a valid animation.
		for (int frameNumber = 2; frameNumber <= MaxIdleFrames; frameNumber++)
		{
			Texture2D next = ResolveIdleFrame(frameOne, frameNumber);
			if (next == null)
				break;
			frames.Add(next);
		}

		return frames;
	}

	// A locked wizard is drawn as his shadow: every opaque pixel of the sprite filled with
	// occlusion, and the pixels on its edge lit as a faint rim, brighter on the side the key light
	// comes from (above and to the left, as everything in the game is lit). Three tones off the
	// Bonelight contract - occlusion, and floor stone `hi` and `lit` (.ai/art-direction.md, section 3)
	// - and none of the wizard's own, so a lock shows his shape and nothing else: no pigment, no
	// staff-head glow, no face. The rim is not decoration; without it the fill, which is darker than
	// the card behind it, read as a hole at 1.2:1 (the old near-black modulate did exactly that).
	private static readonly Color SilhouetteFill = new("#05070C");
	private static readonly Color SilhouetteKeyRim = new("#3A4658");
	private static readonly Color SilhouetteRim = new("#2A3444");

	/// <summary>
	/// The locked-card silhouette of each frame, built from its alpha (see the colours above). Built
	/// per call and never cached in a static - a static Texture2D outlives the engine's own reference
	/// (BonelightSkin's remarks). Each frame is at most 48x48, so it is a few thousand pixel reads.
	/// </summary>
	public static List<Texture2D> BuildSilhouetteFrames(IReadOnlyList<Texture2D> frames)
	{
		var result = new List<Texture2D>();
		if (frames == null)
			return result;

		foreach (Texture2D frame in frames)
		{
			Image source = frame?.GetImage();
			if (source == null || source.IsEmpty())
				continue;
			if (source.IsCompressed())
				source.Decompress();

			int w = source.GetWidth();
			int h = source.GetHeight();
			Image shadow = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
			bool Solid(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && source.GetPixel(x, y).A >= 0.5f;
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					if (!Solid(x, y))
						continue;
					bool openToKey = !Solid(x, y - 1) || !Solid(x - 1, y);
					bool edge = openToKey || !Solid(x + 1, y) || !Solid(x, y + 1);
					shadow.SetPixel(x, y, openToKey ? SilhouetteKeyRim : edge ? SilhouetteRim : SilhouetteFill);
				}
			}
			result.Add(ImageTexture.CreateFromImage(shadow));
		}

		return result;
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