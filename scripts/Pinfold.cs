using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// FF1, THE PINFOLD - the Fallow Fields' spell vault (.ai/achievements-and-secrets.md 8.2). It pays the
// Familiar: a stray the Hayward impounded and nobody came for, learned by letting it out.
//
// SEES. From two chunks out, broken hurdles in the stubble and a dog's prints, all pointing one way (its
// FarTell, LeanToward) - toward a walled stone pound with a barred gate. Behind the bars a hound of warm
// light lies with its head on its paws, breathing: the near tell, the only warm thing in the field.
//
// DOES. Guarded. Coming within WakeRadius wakes two of the Fields' own - the Tithe-Reeve's men, a Rick
// and a Haymaker from the chapter's roster (StageBestiary), as elites through Node2DGame.SpawnEventEnemy,
// so they are as hard as the run is and RespawnEnemy never recycles them. While either stands, the latch
// is dead iron. When both are down the latch takes the glint; walk to it and it lifts, the gate swings in,
// and the hound gets up and runs to him. A fight of a few seconds and a touch: inside the vault's twenty.
//
// PAYS. When the hound reaches him the vault opens (SecretVault.Open): the first time ever the Familiar,
// granted and saved at once, offered from the next level-up; in a later run the same guards and the same
// gate pay a chest from the secret budget.
//
// The pound is solid ground - a body on the terrain layer round its walls, its cells stamped into the
// navigation flags (SolidFootprints) - so feet and bodies stop at the walls and the horde routes round.
// It is never a target and never in "enemies"; the guards are, being the field's own. Its progress lives
// in the run state: State.Step 0 asleep, 1 guards woken, 2 guards down; State.Done opened.
//
// Art: tools/art/secret_fields.py (the pound in three layers, the hound's fourteen frames, the hurdles
// and the prints), on the world's 2-unit grid; what flies is drawn in opaque stops and fades by dither.
public partial class Pinfold : SecretVault
{
	/// <summary>Coming this near the pound wakes its guards.</summary>
	public const float WakeRadius = 420f;

	/// <summary>Walking this near the latch, with the guards down, lifts it.</summary>
	public const float LatchReach = 64f;

	/// <summary>The guards' health, times the run's ordinary spawn health (elites: the bog's tender is 22).</summary>
	public const float GuardHealthMultiplier = 10f;

	/// <summary>The two guards (8.2: "the Tithe-Reeve's men"): the Rick and the Haymaker.</summary>
	public static readonly BestiaryRole[] GuardRoles = { BestiaryRole.Wall, BestiaryRole.Slammer };

	private const string PoundPath = "res://assets/bonelight/world/fields/pinfold.png";
	private const string HoundPath = "res://assets/bonelight/world/fields/pinfold-hound.png";

	// The strips (secret_fields.py at x2). The pound: three 200 x 168 frames - behind, the front shut, the
	// front open - its front wall's foot (art row 78) on the node, so it y-sorts by the wall the player
	// stands in front of. The hound: fourteen 80 x 64 frames (lying, lying, standing, four running; then
	// facing left), lying behind the gate at (-40, -64); its head at (20, -16). The latch at (28, -20).
	private static readonly Vector2 PoundCell = new(200f, 168f);
	private static readonly Vector2 PoundAnchor = new(-100f, -156f);
	private static readonly Vector2 HoundCell = new(80f, 64f);
	private static readonly Vector2 HoundLying = new(-40f, -64f);
	private static readonly Vector2 HoundHead = new(20f, -16f);
	private static readonly Vector2 LatchOffset = new(28f, -20f);
	private static readonly Vector2 HoundFootInCell = new(40f, 58f);
	private const int HoundPoses = 7;

	/// <summary>The walls, in node space: the body that stops feet and bodies.</summary>
	private static readonly Rect2 Walls = new(-88f, -92f, 176f, 92f);

	private static readonly SecretFarTell Tell = new()
	{
		Textures = new[]
		{
			"res://assets/bonelight/world/fields/hurdle-broken-1.png",
			"res://assets/bonelight/world/fields/hurdle-broken-2.png",
			"res://assets/bonelight/world/fields/pawprints-1.png",
		},
		NearTextures = new[]
		{
			"res://assets/bonelight/world/fields/pawprints-1.png",
			"res://assets/bonelight/world/fields/pawprints-2.png",
		},
		InnerRadius = SecretCatalog.VaultFootprint,
		NearRadius = 1536f,
		OuterRadius = 3072f,
		PeakPerChunk = 40,
		NearShare = 0.75f,
		FootFromBottom = 4f,
		LeanToward = true,
	};

	public override SecretFarTell FarTell => Tell;

	public override IEnumerable<(Vector2 Centre, float Radius)> SolidFootprints => new[] { (Position + new Vector2(0f, -46f), 84f) };

	/// <summary>The near tell is the hound's light behind the bars.</summary>
	public override Vector2 TellPosition => GlobalPosition + HoundHead;

	/// <summary>The latch, in world pixels: where he touches it.</summary>
	public Vector2 Latch => GlobalPosition + LatchOffset;

	/// <summary>The guards it woke this run (for the probe). Empty until he arrives.</summary>
	public IReadOnlyList<Enemy> Guards => guards;

	/// <summary>True once the guards are woken and both are down: the latch will lift.</summary>
	public bool GuardsDown => State != null && State.Step >= 2;

	/// <summary>True once the latch has lifted this run.</summary>
	public bool GateOpen => running >= 0f || IsOpened;

	private readonly List<Enemy> guards = new();
	private Texture2D pound;
	private Texture2D hound;
	private Node2D effects;
	private float clock;
	private float running = -1f;       // seconds since the latch lifted, while the hound runs to him
	private Vector2 runFrom;

	private const float StandSeconds = 0.45f;
	private const float RunSeconds = 0.9f;

	public override void _Ready()
	{
		pound = ResourceLoader.Exists(PoundPath) ? GD.Load<Texture2D>(PoundPath) : null;
		hound = ResourceLoader.Exists(HoundPath) ? GD.Load<Texture2D>(HoundPath) : null;
		if (pound == null || hound == null)
			GD.PushError("Pinfold: its art is missing; re-run python tools/art/secret_fields.py, then import.");

		var body = new StaticBody2D
		{
			Name = "Walls",
			CollisionLayer = 1u << (LevelTilePainter.TerrainCollisionLayer - 1),
			CollisionMask = 0,
		};
		body.AddChild(new CollisionShape2D { Position = Walls.GetCenter(), Shape = new RectangleShape2D { Size = Walls.Size } });
		AddChild(body);

		effects = new Node2D
		{
			Name = "Light",
			ZIndex = DrawLayers.Air,
			ZAsRelative = false,
			TopLevel = true,
			Material = SpellInk.DitherByCell,
		};
		effects.Draw += DrawEffects;
		AddChild(effects);
		effects.GlobalPosition = Vector2.Zero;
	}

	public override void _ExitTree()
	{
		if (effects != null && IsInstanceValid(effects))
			effects.Draw -= DrawEffects;
		base._ExitTree();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		Player player = Player;
		SecretDirector.SiteState state = State;

		if (running >= 0f)
		{
			running += dt;
			if (running >= StandSeconds + RunSeconds)
			{
				running = -1f;
				// Rewards in flight are paid even if the boss has come meanwhile (stage-secrets section 4).
				Open(GlobalPosition + new Vector2(0f, 110f));
			}
		}
		else if (player != null && state != null && !state.Done && Active)
		{
			float d = player.GlobalPosition.DistanceTo(GlobalPosition);
			if (state.Step == 0 && d <= WakeRadius)
				Wake();
			else if (state.Step == 1 && guards.TrueForAll(IsDown))
				state.Step = 2;
			else if (state.Step == 2 && player.GlobalPosition.DistanceTo(Latch) <= LatchReach)
				LiftLatch();
		}

		if (player != null && player.GlobalPosition.DistanceSquaredTo(GlobalPosition) < 1600f * 1600f)
		{
			QueueRedraw();
			effects?.QueueRedraw();
		}
	}

	private static bool IsDown(Enemy guard) => guard == null || !IsInstanceValid(guard) || guard.IsDying || guard.Health <= 0;

	// He has come: the Tithe-Reeve's men step out from either side of the pound.
	private void Wake()
	{
		State.Step = 1;
		guards.Clear();
		Node2DGame game = Director.Game;
		for (int i = 0; i < GuardRoles.Length; i++)
		{
			Vector2 at = game.ReachablePoint(GlobalPosition + new Vector2(i == 0 ? -170f : 170f, 40f));
			Enemy guard = game.SpawnEventEnemy(GuardRoles[i], at, GuardHealthMultiplier, true);
			if (guard != null)
				guards.Add(guard);
		}
		SfxPlayer.AtPosition(SfxCatalog.EliteSpawn, GlobalPosition, 0f);
		// Nobody to fight (a chapter with neither role would be a roster bug, reported there): the latch is free.
		if (guards.Count == 0)
			State.Step = 2;
	}

	private void LiftLatch()
	{
		running = 0f;
		runFrom = GlobalPosition + new Vector2(0f, -6f);
		SfxPlayer.AtPosition(SfxCatalog.EventReward, Latch, 0f);
	}

	/// <summary>The guards down and the latch lifted, now: for _UiShot's photographs.</summary>
	public void OpenForProbe()
	{
		if (State.Step < 2)
			State.Step = 2;
		if (!GateOpen)
			LiftLatch();
	}

	// The pound and, between its back and its front, the hound lying behind the bars - on the world's grid
	// (AlignToWorldGrid shifts the drawing by under a pixel; the walls do not move).
	public override void _Draw()
	{
		if (pound == null)
			return;
		SpellInk.AlignToWorldGrid(this);
		DrawTextureRectRegion(pound, new Rect2(PoundAnchor, PoundCell), new Rect2(Vector2.Zero, PoundCell));
		if (!GateOpen && hound != null)
		{
			// Lying, its head on its paws, breathing - a slow breath, never a flicker.
			int pose = ((int)(clock / (GlintPeriod * 0.5f))) % 2;
			DrawTextureRectRegion(hound, new Rect2(HoundLying, HoundCell), new Rect2(new Vector2(pose * HoundCell.X, 0f), HoundCell));
		}
		int front = GateOpen ? 2 : 1;
		DrawTextureRectRegion(pound, new Rect2(PoundAnchor, PoundCell), new Rect2(new Vector2(front * PoundCell.X, 0f), PoundCell));
		DrawSetTransform(Vector2.Zero);
	}

	// World units: the canvas is TopLevel at the origin, through the dither material.
	private void DrawEffects()
	{
		if (!GateOpen)
		{
			// The hound's light through the bars; and the latch's, once there is nobody left to keep it.
			DrawGlint(effects, GlobalPosition + HoundHead, clock, 0.8f);
			if (GuardsDown)
				DrawGlint(effects, Latch, clock + 0.6f, 1f);
			return;
		}
		if (running < 0f || hound == null || Player is not Player player)
			return;

		// It gets up, then runs to him: long and low, four frames to a stride, facing the way it goes.
		Vector2 target = player.GlobalPosition + new Vector2(0f, 10f);
		Vector2 at;
		int pose;
		if (running < StandSeconds)
		{
			at = runFrom;
			pose = 2;
		}
		else
		{
			float t = Mathf.Clamp((running - StandSeconds) / RunSeconds, 0f, 1f);
			at = runFrom.Lerp(target, t * t * (3f - 2f * t));
			pose = 3 + ((int)(running * 12f)) % 4;
		}
		bool left = target.X < runFrom.X;
		int frame = pose + (left ? HoundPoses : 0);
		Vector2 corner = SpellInk.Snap(at - HoundFootInCell);
		effects.DrawTextureRectRegion(hound, new Rect2(corner, HoundCell), new Rect2(new Vector2(frame * HoundCell.X, 0f), HoundCell));
		DrawGlint(effects, at + new Vector2(left ? -14f : 14f, -40f), 0.6f, 1f);
	}
}
