using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// THE GLEANING BELL: walk onto it and every XP orb on the map flies to you.
//
// Eric: "there should be an item or spell or boon or collectible item that collects all missed XP.
// (i dont want it to be required, so people take it every run)". The genre's answer is Vampire
// Survivors' Vacuum - not a choice but a find - and that is what this is: no level-up offers it, no
// chest card names it, nothing raises its chance, so there is nothing to build around and nothing to
// take every run. It is carried by the bodies that carry chests (an elite, a Warden, the Sketchbook's
// ghost) at a chance that rises with every one that paid none (DropTable, Node2DGame.GleaningBell.cs),
// so a run finds one or two. .ai/passives-and-items.md section 8i.
//
// In an English village the gleaning bell was rung to say when the poor could go into the reaped
// fields and gather what the harvest had left; here it calls in what the player walked past.
//
// WHAT IT PULLS: XP orbs and nothing else - not a flask, not a potion, never a chest. It is an XP tool,
// and a heal that came with it would make it a panic button worth holding off for. Every orb in the
// group, however far, the far bank included (Node2DGame.SweepFarXpOrbs); orbs dropped after the ring
// are ordinary orbs. The pull is XPOrb.Glean: the near ones land first and the far ones stream in
// behind, the whole map in about two seconds.
//
// A HAUL WORTH SEVERAL LEVELS queues them, one screen at a time (Node2DGame.ResumeOrOpenNextMenu): the
// first orb over the line opens the level-up and pauses the run, the orbs still in flight freeze where
// they are, and they land and pay the next level once the screen is closed. Player.AddXp pays at most
// one level per orb, so a haul can never put two screens up at once.
//
// Code-only, no scene: a Sprite2D stepping the four frames of tools/art/sprite_bell.py, a light layer,
// and a circle to step on. It is not a PlaceholderShape pickup, because PickupBase's hover rotates and
// scales that node, which on 16px art at x2 is the sub-pixel crawl .ai/art-direction.md forbids; the
// breath is in the frames instead, as the orbs' is.
public partial class GleaningBell : PickupBase
{
	public const string TexturePath = "res://assets/bonelight/pickups/gleaning-bell.png";

	/// <summary>Every bell lying in the world. The run lets only one lie there at a time.</summary>
	public static readonly List<GleaningBell> Live = new();

	/// <summary>Where it was thrown from (its carrier's body). Used only while <see cref="TossSeconds"/> is above 0.</summary>
	public Vector2 TossFrom { get; set; }

	/// <summary>Seconds in the air before it lands and can be taken; 0 for a bell that is simply there.</summary>
	public float TossSeconds { get; set; }

	/// <summary>True once rung: the orbs are already on their way.</summary>
	public bool Taken { get; private set; }

	/// <summary>True while it is still in the air.</summary>
	public bool Tossing => tossing;

	private const int Frames = 4;
	private const float FramesPerSecond = 6f;
	private const float Cycle = Frames / FramesPerSecond;
	// The cell is 16x16 drawn at x2 and centred, so the seat on row 14 is 6.5 art pixels below its
	// centre: the sprite sits 13 px up, which puts the node - the y-sort key - on its foot.
	private static readonly Vector2 SpriteRest = new Vector2(0f, -13f);
	private const float TossHeight = 30f;
	private const float TakeRadius = 14f;

	// Grass, the small orb's ramp (.ai/art-direction.md section 3): mid #9BD455, hot #CFEE94.
	private static readonly Color GrassMid = new Color(0.608f, 0.831f, 0.333f);
	private static readonly Color GrassHot = new Color(0.812f, 0.933f, 0.580f);

	private Sprite2D sprite;
	private Node2D lightLayer;
	private float clock;
	private float tossAge;
	private bool tossing;
	private Vector2 landAt;

	public override void _EnterTree() => Live.Add(this);

	public override void _ExitTree()
	{
		Live.Remove(this);
		if (lightLayer != null && IsInstanceValid(lightLayer))
			lightLayer.Draw -= OnLightDraw;
	}

	public override void _Ready()
	{
		// PickupBase's idle motion has no node to move (there is no PlaceholderShape), and these say so.
		HoverAmplitude = 0f;
		SpinSpeed = 0f;
		PulseStrength = 0f;

		var texture = GD.Load<Texture2D>(TexturePath);
		if (texture == null)
			GD.PushError($"GleaningBell: {TexturePath} is missing or not imported; only its light will show. "
				+ "Re-run: python tools/art/sprite_bell.py, then import.");
		sprite = new Sprite2D
		{
			Name = "Bell",
			Texture = texture,
			Hframes = Frames,
			Scale = new Vector2(2f, 2f),
			Position = SpriteRest,
		};
		AddChild(sprite);

		lightLayer = new Node2D
		{
			Name = "Light",
			ZIndex = 3,
			Material = ExploreArt.Additive,
			TextureFilter = TextureFilterEnum.Linear,
		};
		lightLayer.Draw += OnLightDraw;
		AddChild(lightLayer);

		AddChild(new CollisionShape2D
		{
			Name = "CollisionShape2D",
			Position = new Vector2(0f, -10f),
			Shape = new CircleShape2D { Radius = TakeRadius },
		});

		base._Ready();

		if (TossSeconds > 0f)
		{
			// Thrown out of the body that carried it. Not takeable and not drawn to the magnet until it
			// lands: an elite usually dies touching the player, and a bell rung on the frame it appeared
			// would be a sound and a stream with no idea where either came from.
			tossing = true;
			landAt = Position;
			Position = TossFrom;
			Monitoring = false;
			MagnetAttracted = false;
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		base._Process(delta);

		// The breath: frames 1 and 2 are the bright ones, and the light's crest is centred on them.
		float phase = Mathf.PosMod(clock / Cycle, 1f);
		if (sprite != null)
			sprite.Frame = Mathf.FloorToInt(phase * Frames) % Frames;
		lightLayer?.QueueRedraw();

		if (!tossing)
			return;
		tossAge += dt;
		float t = Mathf.Clamp(tossAge / TossSeconds, 0f, 1f);
		Position = TossFrom.Lerp(landAt, t);
		// The hop, in whole art pixels (two world pixels): a float offset resamples the sprite every frame.
		float lift = Mathf.Round(4f * t * (1f - t) * TossHeight / 2f) * 2f;
		if (sprite != null)
			sprite.Position = SpriteRest - new Vector2(0f, lift);
		if (t < 1f)
			return;
		tossing = false;
		if (sprite != null)
			sprite.Position = SpriteRest;
		// _Process is outside any physics callback, so these can be written directly (ChestReward does).
		Monitoring = true;
		MagnetAttracted = true;
	}

	protected override void OnPickedUp(Node player)
	{
		if (player is Player hero)
			Ring(hero);
	}

	/// <summary>
	/// Rung: every XP orb on the map flies to <paramref name="hero"/>. Public so the screenshot harness
	/// can ring one on a chosen frame; in a run it is rung by walking onto it.
	/// </summary>
	public void Ring(Player hero)
	{
		if (Taken || tossing || hero == null || !IsInstanceValid(hero))
			return;
		Taken = true;
		// Usually inside body_entered, where Godot refuses a direct write to Monitoring.
		SetDeferred(Area2D.PropertyName.Monitoring, false);

		// One walk of the group, once, on a rare event: the only allocation is the group's array.
		int orbs = 0;
		int value = 0;
		float farthest = 0f;
		Vector2 at = hero.GlobalPosition;
		foreach (Node node in GetTree().GetNodesInGroup(XPOrb.GroupName))
		{
			if (node is not XPOrb orb || orb.IsQueuedForDeletion())
				continue;
			orb.Glean();
			orbs++;
			value += orb.Value;
			farthest = Mathf.Max(farthest, orb.GlobalPosition.DistanceTo(at));
		}

		// Six accelerating blips: generated for exactly this, "a vacuum pull beginning - once per pull,
		// not per orb" (.ai/audio-manifest.md). Each orb then lands with its own pickup_xp, whose pitch
		// climbs with the streak (SfxPlayer.PickupXp), so the stream is heard arriving as well as seen.
		SfxPlayer.Global(SfxCatalog.PickupMagnet);
		PickupFlourish.Play(hero, PickupFlourish.Kind.Glean);
		GD.Print($"GLEANING BELL rung at {Node2DGame.Current?.RunSeconds ?? 0f:0.0}s: {orbs} orb(s) worth {value} XP before multipliers, "
			+ $"the farthest {farthest:0} px off; level {hero.CurrentLevel}, {hero.CurrentXP}/{hero.XPToNextLevel}");
		QueueFree();
	}

	private void OnLightDraw()
	{
		if (lightLayer == null)
			return;
		// 0.7 to 1.0 on the frames' cycle, crest on frames 1 and 2 (phase 0.5).
		float breath = 0.85f - 0.15f * Mathf.Cos(Mathf.Tau * clock / Cycle);
		Vector2 lip = sprite != null ? sprite.Position + new Vector2(0f, 12f) : Vector2.Zero;
		// The light the bell holds, spilling round its lip: soft and small, so it is found by looking
		// and does not read as a spell going off.
		ExploreArt.DrawGlow(lightLayer, lip, 46f, new Color(GrassMid, 0.16f * breath));
		ExploreArt.DrawGlow(lightLayer, lip, 18f, new Color(GrassHot, 0.30f * breath));
	}
}
