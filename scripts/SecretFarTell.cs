using Godot;
using System;
using WizardSurvivors.scripts;

// THE FAR TELL (.ai/stage-secrets.md section 2; .ai/achievements-and-secrets.md 6.4): the ground changes
// toward a secret - crystals grow commoner toward the Geode Heart, stumps thicken toward the First Tree -
// over a chunk or two, before anything of the secret itself is on screen. A gradient and never a ring
// with an edge: a hard edge is a line the player learns to stand on.
//
// It is decor, furnished with the chunk it lies in (SecretDirector.FurnishChunk, called by Node2DGame's
// furnishing beside the curated props), so it streams, is forgotten and comes back with its chunk like
// every other rock. Its own stream of rolls from (terrain seed, chunk, salt), every roll drawn before its
// spot is tested, so a chunk walked back to is furnished the same - and nothing in it depends on the run.
// It keeps every rule a scattered sprite keeps (Node2DGame.IsDecorSpotOpen): not on water, a wall or a
// hazard, not in a landmark or a site's footprint, not in the spawn clearing.
//
// What the near band does: from NearRadius in, a growing share of the pieces wear NearTextures instead -
// the cracked crystals round the Geode Heart - so the gradient says "nearer" twice: more of them, then
// different ones.
//
// No collision: these are a direction to walk in, not more things to snag on in the fight round the site.
public sealed class SecretFarTell
{
	/// <summary>The chapter's own decor, as res:// paths: what grows commoner toward the site.</summary>
	public string[] Textures { get; init; } = Array.Empty<string>();
	/// <summary>What the nearest pieces become (optional).</summary>
	public string[] NearTextures { get; init; } = Array.Empty<string>();
	/// <summary>Nothing closer than this: the site's own ground, which its footprint already keeps.</summary>
	public float InnerRadius { get; init; } = 300f;
	/// <summary>Inside this the near textures start (a chunk).</summary>
	public float NearRadius { get; init; } = 1536f;
	/// <summary>Where the gradient fades to nothing (two chunks).</summary>
	public float OuterRadius { get; init; } = 3072f;
	/// <summary>How many pieces a whole chunk at the inner edge would hold. The density falls off from there.</summary>
	public int PeakPerChunk { get; init; } = 40;
	/// <summary>At the inner edge, the share of pieces that wear a near texture.</summary>
	public float NearShare { get; init; } = 0.8f;
	/// <summary>Whole-number scales only (the x2 contract): the sprites are already authored at x2.</summary>
	public int MinScale { get; init; } = 1;
	public int MaxScale { get; init; } = 1;
	/// <summary>Texture pixels from the bottom edge up to the contact shadow's middle: where it stands.</summary>
	public float FootFromBottom { get; init; } = 10f;
	/// <summary>
	/// True for pieces drawn pointing RIGHT that should point at the site - hoofprints walking to the Pale
	/// Hart's clearing, bracken bent over by what went that way: each is flipped to face it, instead of by
	/// its rolled coin. The coin is still rolled, so nothing after it moves (added by the vaults' agent,
	/// 2026-10-30; false keeps every tell before it exactly as it was).
	/// </summary>
	public bool FaceSite { get; init; }

	private Texture2D[] far;
	private Texture2D[] near;

	private const int SaltFarTell = 0x7E11;

	/// <summary>
	/// Scatters this tell's share of one chunk round <paramref name="site"/> into <paramref name="parent"/>
	/// (the chunk's y-sorted prop holder). <paramref name="salt"/> keeps two tells in one chunk apart.
	/// </summary>
	public void Furnish(Node2DGame game, Vector2 site, Vector2I chunk, Node2D parent, Rect2 rect, int salt)
	{
		if (game == null || parent == null || !Reaches(rect, site))
			return;
		if (!Load())
			return;

		var rng = new LevelGenerator.Rng(LevelGenerator.Hash(game.TerrainSeed, chunk.X, chunk.Y, SaltFarTell + salt));
		float span = Mathf.Max(1f, OuterRadius - InnerRadius);
		float nearSpan = Mathf.Max(1f, NearRadius - InnerRadius);
		for (int i = 0; i < PeakPerChunk; i++)
		{
			// Every roll first, so a spot that fails never shifts the rolls of the ones after it.
			var at = new Vector2(rect.Position.X + rng.NextFloat() * rect.Size.X, rect.Position.Y + rng.NextFloat() * rect.Size.Y);
			float keep = rng.NextFloat();
			float nearRoll = rng.NextFloat();
			int farIndex = rng.Next(far.Length);
			int nearIndex = near.Length > 0 ? rng.Next(near.Length) : 0;
			bool flip = rng.NextFloat() < 0.5f;
			int scale = MinScale + rng.Next(Math.Max(1, MaxScale - MinScale + 1));

			float d = at.DistanceTo(site);
			if (d < InnerRadius || d > OuterRadius)
				continue;
			// Quadratic: thick at the site, a stray or two two chunks out - a gradient, never a ring.
			float t = 1f - (d - InnerRadius) / span;
			if (keep >= t * t)
				continue;
			if (!game.IsDecorSpotOpen(at))
				continue;

			bool cracked = near.Length > 0 && d < NearRadius && nearRoll < NearShare * (1f - (d - InnerRadius) / nearSpan);
			Texture2D texture = cracked ? near[nearIndex] : far[farIndex];
			// Stood on its foot, so the chunk holder's y-sort puts the wizard in front of it south of it.
			var sprite = new Sprite2D
			{
				Name = cracked ? "SecretFarTellNear" : "SecretFarTell",
				Texture = texture,
				Centered = true,
				Position = at.Round(),
				Offset = new Vector2(0f, -texture.GetHeight() * 0.5f + FootFromBottom),
				Scale = new Vector2(scale, scale),
				FlipH = FaceSite ? site.X < at.X : flip,
			};
			sprite.AddToGroup(cracked ? GroupNear : Group);
			parent.AddChild(sprite);
		}
	}

	/// <summary>Every far tell's sprite is in one of these, so a probe can count the gradient.</summary>
	public const string Group = "secret_far_tell";
	public const string GroupNear = "secret_far_tell_near";

	private bool Reaches(Rect2 rect, Vector2 site)
	{
		float dx = MathF.Max(MathF.Max(rect.Position.X - site.X, 0f), site.X - rect.End.X);
		float dy = MathF.Max(MathF.Max(rect.Position.Y - site.Y, 0f), site.Y - rect.End.Y);
		return dx * dx + dy * dy <= OuterRadius * OuterRadius;
	}

	private bool Load()
	{
		if (far != null)
			return far.Length > 0;
		far = LoadAll(Textures);
		near = LoadAll(NearTextures);
		if (far.Length == 0)
			GD.PushError($"SecretFarTell: none of its textures loaded ({string.Join(", ", Textures)}); is the art imported?");
		return far.Length > 0;
	}

	private static Texture2D[] LoadAll(string[] paths)
	{
		var loaded = new System.Collections.Generic.List<Texture2D>();
		foreach (string path in paths ?? Array.Empty<string>())
		{
			var texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
			if (texture != null)
				loaded.Add(texture);
			else
				GD.PushError($"SecretFarTell: '{path}' did not load.");
		}
		return loaded.ToArray();
	}
}
