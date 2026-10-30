using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// S5, THE SWALLOW-HOLE - the Blighted Swamp's spell vault (.ai/achievements-and-secrets.md 8.6). It pays
// Gravewell, the grave that opens under the swarm and pulls it in - learned from a hole in the bog that
// does the same to anything that walks into it.
//
// SEES. From two chunks out the bog's reeds grow commoner and every one of them leans toward one place,
// and from a chunk out they stand in puddles of black mud (its FarTell). Then the place: a sinkhole of
// black mud 160 px across in a stain of black bog, ringed by reeds bowed in over it, its mud turning slowly
// in toward the middle, bubbles working up - and at the bottom, deep down, the warm glint breathing.
//
// DOES. Lure (SecretLure): an ordinary enemy whose feet enter the mud is taken - out of "enemies" that
// frame, gone, no death, no XP, no kill - and counted. Thirty and it is full. Never an elite, a miniboss
// or a boss. It is NOT blocking ground: the shared flow field runs straight through it, so a horde chasing a
// wizard who stands at the rim walks into it. Standing at the edge and letting them come is the whole ask.
// So it is a hazard to HIM instead (the vault's own rule, not the verb's): wading in slows him to a crawl
// (WadeSpeedShare) and costs a point of health a second, through Player.TakeDamage like the lava's.
//
// PAYS. Full, the middle heaves up into a wet dome, breaks, and gives up what it held: the light rises out
// of it and goes to him (SecretVault.Open) - the first time ever Gravewell, granted and saved at once and
// offered from the next level-up; in a later run the same beat and a chest from the secret budget. Then the
// mud lies still: sated, no pull, and no hazard.
//
// The mud is scenes/shaders/swallow_hole.gdshader on one quad (the mire's way), the reeds round the rim and
// the far tell are tools/art/secret_swamp.py; the splashes and the light are stamped cells (VaultLight).
public partial class SwallowHole : SecretVault
{
	/// <summary>The trigger: an enemy's feet this near the middle are taken (8.6: 160 px across).</summary>
	public const float HoleRadius = 80f;

	/// <summary>How many fill it (8.6: thirty).</summary>
	public const int NeededTaken = 30;

	/// <summary>The black stain round it, drawn out to here (the near band of "bog to black mud").</summary>
	public const float ApronRadius = 300f;

	/// <summary>Wading: the share of his step he keeps in the mud ("a crawl"), and what it costs.</summary>
	public const float WadeSpeedShare = 0.3f;
	public const int WadeDamage = 1;
	public const float WadeDamageSeconds = 1f;

	// His feet count as in the mud a little inside the trigger, so the rim he is asked to stand on is safe.
	private const float WadeInset = 6f;

	private const string ShaderPath = "res://scenes/shaders/swallow_hole.gdshader";
	private const string ArtDir = "res://assets/bonelight/world/swamp/";

	// tools/art/secret_swamp.py: FOOT_MARGIN art pixels under every foot, at x2 to the middle of that row.
	public const float FootFromBottom = 11f;

	/// <summary>The far tell, one instance (its textures load once): 8.6, "Sees".</summary>
	private static readonly SecretFarTell Tell = new()
	{
		Textures = new[] { ArtDir + "reed-lean-1.png", ArtDir + "reed-lean-2.png" },
		NearTextures = new[] { ArtDir + "reed-mire-1.png", ArtDir + "reed-mire-2.png" },
		InnerRadius = SecretCatalog.VaultFootprint,
		NearRadius = 1536f,
		OuterRadius = 3072f,
		// The bog has its own reeds at about a dozen a screen; these have to read as MORE, and leaning.
		PeakPerChunk = 60,
		NearShare = 0.8f,
		FootFromBottom = FootFromBottom,
		LeanToward = true,
	};

	public override SecretFarTell FarTell => Tell;

	// Nothing solid: the horde must be able to walk in (8.6). SolidFootprints stays empty.

	/// <summary>How many it has taken this run (the lure's count). Public for the probe.</summary>
	public int Taken => State?.Step ?? 0;

	public bool IsFull => Taken >= NeededTaken;

	/// <summary>True once it has given the light up and lies still.</summary>
	public bool IsSated => State?.Done == true || (IsFull && heave < 0f && !light.InFlight);

	public SecretLure Lure { get; private set; }

	private Node2D mud;
	private ShaderMaterial mudMaterial;
	private Node2D effects;
	private float clock;
	private float heave = -1f;
	private readonly VaultLight light = new();
	private readonly Vector4[] gulps = new Vector4[8];
	private int gulpNext;
	private readonly List<(Vector2 At, float Born, float Seed, bool Big)> splashes = new();
	private Vector2 lastHeroPosition;
	private bool heroWasWading;
	private float wadeClock;

	private static readonly StringName ParamCentre = "centre";
	private static readonly StringName ParamHole = "hole_radius";
	private static readonly StringName ParamApron = "apron_radius";
	private static readonly StringName ParamTime = "time";
	private static readonly StringName ParamFullness = "fullness";
	private static readonly StringName ParamGlint = "glint";
	private static readonly StringName ParamHeave = "heave";
	private static readonly StringName ParamSated = "sated";
	private static readonly StringName ParamGulps = "gulps";
	private static readonly StringName ParamGulpCount = "gulp_count";

	// The rim's reeds: where each stands (from the middle) and which of the three it is. Up the back and the
	// sides only: a reed is drawn standing up from its foot, so one on the near bank would stand across the
	// hole's face and hide the mud and the glint the player is meant to see.
	private static readonly (Vector2 At, int Kind)[] RimReeds =
	{
		(new Vector2(-104f, -30f), 0), (new Vector2(-92f, -62f), 1), (new Vector2(-58f, -88f), 2),
		(new Vector2(-14f, -98f), 1), (new Vector2(36f, -94f), 2), (new Vector2(76f, -70f), 0),
		(new Vector2(104f, -26f), 1), (new Vector2(112f, 18f), 2), (new Vector2(-114f, 20f), 2),
	};

	public override void _Ready()
	{
		// The rim's reeds sort among the bodies, each at its own foot.
		YSortEnabled = true;
		// After the player has moved (his step is what the mud takes back).
		ProcessPhysicsPriority = 100;

		var shader = ResourceLoader.Exists(ShaderPath) ? GD.Load<Shader>(ShaderPath) : null;
		if (shader == null)
			GD.PushError($"SwallowHole: {ShaderPath} did not load.");
		mudMaterial = new ShaderMaterial { Shader = shader };
		mud = new Node2D { Name = "Mud", ZIndex = DrawLayers.Ground, ZAsRelative = false, Material = mudMaterial };
		mud.Draw += DrawMud;
		AddChild(mud);

		for (int i = 0; i < RimReeds.Length; i++)
		{
			var (at, kind) = RimReeds[i];
			string path = ArtDir + $"rim-reed-{kind + 1}.png";
			Texture2D texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
			if (texture == null)
			{
				GD.PushError($"SwallowHole: {path} is missing; re-run python tools/art/secret_swamp.py, then import.");
				continue;
			}
			// Authored leaning to +x: the ones east of the middle are flipped, so every reed bows in. The foot
			// is 16 texture pixels in from the left edge (rim_reed's cx 8, at x2); a flip mirrors the texture
			// inside the same rect, so its offset is mirrored too.
			bool flip = at.X > 0f;
			float footX = texture.GetWidth() * 0.5f - 16f;
			AddChild(new Sprite2D
			{
				Name = $"Reed{i}",
				Texture = texture,
				Centered = true,
				Position = at,
				Offset = new Vector2(flip ? -footX : footX, -texture.GetHeight() * 0.5f + FootFromBottom),
				FlipH = flip,
			});
		}

		Lure = new SecretLure
		{
			Name = "Lure",
			Director = Director,
			State = State,
			Radius = HoleRadius,
			Needed = NeededTaken,
		};
		Lure.Took += OnTook;
		Lure.Filled += OnFilled;
		AddChild(Lure);

		// The splashes and the light fly over everything, on the world's grid.
		effects = new Node2D { Name = "Light", ZIndex = DrawLayers.Air, ZAsRelative = false, TopLevel = true };
		effects.Draw += DrawEffects;
		AddChild(effects);
		effects.GlobalPosition = Vector2.Zero;

		mudMaterial.SetShaderParameter(ParamCentre, GlobalPosition);
		mudMaterial.SetShaderParameter(ParamHole, HoleRadius);
		mudMaterial.SetShaderParameter(ParamApron, ApronRadius);
		UploadGulps();
	}

	public override void _ExitTree()
	{
		if (mud != null && IsInstanceValid(mud))
			mud.Draw -= DrawMud;
		if (effects != null && IsInstanceValid(effects))
			effects.Draw -= DrawEffects;
		if (Lure != null && IsInstanceValid(Lure))
		{
			Lure.Took -= OnTook;
			Lure.Filled -= OnFilled;
		}
		base._ExitTree();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		if (heave >= 0f)
		{
			heave += dt;
			// The dome breaks at 0.75 s: the light comes out of it.
			if (heave >= 0.75f && heave - dt < 0.75f)
				light.Release(GlobalPosition);
			if (heave > 1.2f)
				heave = -1f;
		}
		if (light.Tick(dt))
			// Rewards in flight are paid even if the boss has come meanwhile (stage-secrets section 4).
			Open(GlobalPosition + new Vector2(0f, HoleRadius + 70f));
		for (int i = splashes.Count - 1; i >= 0; i--)
			if (clock - splashes[i].Born > 0.6f)
				splashes.RemoveAt(i);

		Player player = Player;
		bool near = player != null && player.GlobalPosition.DistanceSquaredTo(GlobalPosition) < 1800f * 1800f;
		if (!near && heave < 0f && !light.InFlight)
			return;
		bool sated = State?.Done == true;
		float fullness = Mathf.Clamp(Taken / (float)NeededTaken, 0f, 1f);
		mudMaterial.SetShaderParameter(ParamTime, clock);
		mudMaterial.SetShaderParameter(ParamFullness, fullness);
		mudMaterial.SetShaderParameter(ParamGlint, IsFull ? 0f : 0.55f + 0.45f * fullness);
		mudMaterial.SetShaderParameter(ParamHeave, heave);
		mudMaterial.SetShaderParameter(ParamSated, sated || (IsFull && heave < 0f) ? 1f : 0f);
		effects.QueueRedraw();
	}

	public override void _PhysicsProcess(double delta)
	{
		Player player = Player;
		if (player == null || !IsInstanceValid(player))
			return;
		// Wading: his feet in the mud. It holds his step back to a crawl and stings once a second. Gone once
		// it has been sated - a full hole is only a black pool.
		bool wading = !IsSated && player.FootPosition.DistanceTo(GlobalPosition) < HoleRadius - WadeInset;
		Vector2 now = player.GlobalPosition;
		Vector2 step = now - lastHeroPosition;
		// Only an ordinary step is held back: a teleport (a probe, a respawn) is left alone.
		if (wading && heroWasWading && step.LengthSquared() < 60f * 60f)
			player.GlobalPosition = lastHeroPosition + step * WadeSpeedShare;
		lastHeroPosition = player.GlobalPosition;
		heroWasWading = wading;

		if (wading && !player.IsDead)
		{
			wadeClock += (float)delta;
			if (wadeClock >= WadeDamageSeconds)
			{
				wadeClock -= WadeDamageSeconds;
				player.TakeDamage(WadeDamage);
			}
		}
		else
			wadeClock = 0f;
	}

	/// <summary>True while his feet are in the mud. For the probe.</summary>
	public bool IsWading => heroWasWading;

	// One went in: the mud closes over it with a ring, and a little of it is thrown up.
	private void OnTook(Vector2 at)
	{
		gulps[gulpNext] = new Vector4(at.X, at.Y, clock, gulpNext * 1.37f);
		gulpNext = (gulpNext + 1) % gulps.Length;
		UploadGulps();
		splashes.Add((at, clock, clock * 3.1f, false));
		SfxPlayer.Impact(Element.Water, at);
	}

	// The thirtieth: it heaves, breaks, and the light comes out (in _Process, on the beat).
	private void OnFilled()
	{
		heave = 0f;
		splashes.Add((GlobalPosition, clock + 0.75f, 2.3f, true));
		SfxPlayer.Impact(Element.Earth, GlobalPosition);
	}

	private void UploadGulps()
	{
		// Only when one goes in, never per frame: a Variant of an array allocates (mire_surface's note).
		mudMaterial?.SetShaderParameter(ParamGulps, gulps);
		mudMaterial?.SetShaderParameter(ParamGulpCount, gulps.Length);
	}

	/// <summary>
	/// For _UiShot's photographs (--secret-stage=N), never in play: 1 half full with something going in, 2
	/// nearly full, 3 full - the heave, the light and the pay.
	/// </summary>
	public void PoseForProbe(int stage)
	{
		if (State == null)
			return;
		if (stage >= 3)
		{
			State.Step = NeededTaken - 1;
			OnTook(GlobalPosition + new Vector2(-30f, 12f));
			State.Step = NeededTaken;
			OnFilled();
			return;
		}
		State.Step = stage <= 0 ? 0 : stage == 1 ? NeededTaken / 2 : NeededTaken - 3;
		if (stage >= 1)
		{
			OnTook(GlobalPosition + new Vector2(34f, -18f));
			OnTook(GlobalPosition + new Vector2(-46f, 26f));
		}
	}

	// --- drawing -------------------------------------------------------------------------------------

	private void DrawMud()
	{
		// One quad over the apron; the shader shades every cell of it.
		mud.DrawRect(new Rect2(-ApronRadius, -ApronRadius, ApronRadius * 2f, ApronRadius * 2f), Colors.White);
	}

	private static readonly Color MudDark = new(0.043f, 0.059f, 0.094f);   // stone deep #0B0F18
	private static readonly Color MudWet = new(0.165f, 0.306f, 0.345f);    // lichen base #2A4E58

	private void DrawEffects()
	{
		foreach (var (at, born, seed, big) in splashes)
		{
			float t = (clock - born) / 0.6f;
			VaultLight.DrawThrow(effects, at, t, big ? 18 : 7, big ? HoleRadius * 0.9f : 22f, MudDark, MudWet, seed);
		}
		if (light.InFlight && Player is Player player)
			light.Draw(effects, player.GlobalPosition + new Vector2(0f, -40f));
	}
}
