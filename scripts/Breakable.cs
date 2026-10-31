using Godot;
using System;
using WizardSurvivors.scripts;

// A thing broken by the fighting round it (.ai/achievements-and-secrets.md section 5, "Things to break":
// the giant crystal, a lightning-split oak, a cracked flagstone, a cart under sand). Every point of damage
// the player lands on an enemy within Reach of its foot counts (a StrikeListener); Needed of it breaks it,
// in stages the player can see - whole, then hairline, cracked, splitting, then broken.
//
// It is NEVER a target and never joins "enemies" (the breakables rule, the thaw's), so it costs the
// loadout nothing: the swarm brings the damage to wherever the player stands, and standing at its foot
// and fighting is breaking it.
//
// Its progress is the owner's run state (State.Progress: weighted damage landed; State.Done: broken),
// never its own, so a breakable on a chunk that is forgotten and furnished again is as cracked as it was.
// It listens only while the player is within ListenRange and the owner says it may (CanProgress: a secret
// stands down with the boss), so a breakable nobody is near costs nothing per hit.
//
// Drawn from a strip (Sheet, StageCount + 2 frames: whole, each crack stage, broken) if it has one, or in
// code - the thaw's cracks, one more per stage - if it does not. A hit that counts jolts it a pixel, the
// only feedback it gives besides the cracks: nothing on screen says how far along it is.
public partial class Breakable : Node2D
{
	/// <summary>Weighted damage that breaks it.</summary>
	public float Needed { get; set; } = 1000f;

	/// <summary>Hits on enemies standing within this of its foot (this node) count.</summary>
	public float Reach { get; set; } = 140f;

	/// <summary>The crack stages between whole and broken: hairline, cracked, splitting.</summary>
	public int StageCount { get; set; } = 3;

	/// <summary>What a hit from this spell is worth per point, or null for 1 (StrikeListener.Weight).</summary>
	public Func<SpellData, float> Weight { get; set; }

	/// <summary>Where the progress lives - the owner's run state. Required.</summary>
	public SecretDirector.SiteState State { get; set; }

	/// <summary>False to freeze it (a secret standing down). Null: always.</summary>
	public Func<bool> CanProgress { get; set; }

	/// <summary>Who to measure ListenRange from.</summary>
	public Func<Node2D> Watcher { get; set; }

	/// <summary>It listens only while the watcher is this near.</summary>
	public float ListenRange { get; set; } = 1400f;

	/// <summary>A strip of StageCount + 2 frames left to right: whole, each crack stage, broken. Optional.</summary>
	public Texture2D Sheet { get; set; }

	/// <summary>The frame's top-left relative to the foot, in pixels (the sheet's anchor).</summary>
	public Vector2 SheetOffset { get; set; }

	/// <summary>Called once, the frame after it breaks (never inside the hit that broke it).</summary>
	public event Action Broke;

	/// <summary>Called when it reaches a new stage, with the stage (1 = the first crack).</summary>
	public event Action<int> StageReached;

	public float Progress => State == null ? 0f : Mathf.Clamp(State.Progress / Mathf.Max(1f, Needed), 0f, 1f);

	public bool IsBroken => State != null && State.Done;

	/// <summary>
	/// 0 whole (nothing has landed yet), 1 to StageCount cracking, StageCount + 1 broken. The first crack
	/// shows on the first hit that counts, so the player learns at once that fighting here does something.
	/// </summary>
	public int Stage => StageOf(State?.Progress ?? 0f, IsBroken);

	public int FrameCount => StageCount + 2;

	private StrikeListener strike;
	private Sprite2D sprite;
	private int shownStage = -1;
	private bool breakPending;
	private float jolt;

	public int StageOf(float landed, bool broken)
	{
		if (broken)
			return StageCount + 1;
		if (landed <= 0f)
			return 0;
		return Math.Min(StageCount, 1 + (int)(StageCount * Mathf.Clamp(landed / Mathf.Max(1f, Needed), 0f, 0.9999f)));
	}

	public override void _Ready()
	{
		State ??= new SecretDirector.SiteState();
		strike = new StrikeListener(GlobalPosition, Reach) { Weight = Weight, Struck = OnStruck };
		if (Sheet != null)
		{
			sprite = new Sprite2D
			{
				Name = "Stages",
				Texture = Sheet,
				Hframes = FrameCount,
				Centered = false,
				Offset = SheetOffset,
			};
			AddChild(sprite);
		}
		shownStage = Stage;
		if (sprite != null)
			sprite.Frame = shownStage;
	}

	public override void _ExitTree() => strike?.Stop();

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		if (breakPending)
		{
			breakPending = false;
			Broke?.Invoke();
		}
		if (jolt > 0f)
		{
			jolt = MathF.Max(0f, jolt - dt);
			if (sprite != null)
				sprite.Position = jolt > 0f ? new Vector2(((int)(jolt * 60f) % 2 == 0) ? 1f : -1f, 0f) : Vector2.Zero;
		}

		bool near = Watcher?.Invoke() is Node2D watcher && IsInstanceValid(watcher)
			&& watcher.GlobalPosition.DistanceSquaredTo(GlobalPosition) <= ListenRange * ListenRange;
		bool want = !IsBroken && near && CanProgress?.Invoke() != false;
		if (want)
		{
			strike.Centre = GlobalPosition;
			strike.Start();
		}
		else
			strike.Stop();

		int stage = Stage;
		if (stage != shownStage)
		{
			shownStage = stage;
			if (sprite != null)
				sprite.Frame = Math.Min(stage, FrameCount - 1);
			QueueRedraw();
		}
	}

	private void OnStruck(float amount, Vector2 where)
	{
		if (IsBroken || CanProgress?.Invoke() == false)
			return;
		int before = Stage;
		State.Progress += amount;
		jolt = 0.1f;
		if (State.Progress >= Needed)
		{
			State.Progress = Needed;
			State.Done = true;
			strike.Stop();
			breakPending = true;
		}
		int after = Stage;
		if (after != before && after <= StageCount)
			StageReached?.Invoke(after);
	}

	/// <summary>
	/// Breaks it now, as if the last hit had landed: for a probe that photographs the after, and for
	/// nothing in play.
	/// </summary>
	public void BreakNow()
	{
		if (IsBroken)
			return;
		State.Progress = Needed;
		State.Done = true;
		strike?.Stop();
		breakPending = true;
	}

	public override void _Draw()
	{
		if (sprite != null || shownStage <= 0)
			return;
		// No sheet: the thaw's cracks, two more a stage, from the foot up.
		DrawCracks(this, new Vector2(0f, -Reach * 0.25f), Reach * 0.3f, Math.Min(5, shownStage * 2), new Color(0.78f, 0.92f, 1.0f, 0.95f));
	}

	/// <summary>
	/// Cracks radiating from <paramref name="centre"/>: <paramref name="count"/> straight lines out to
	/// <paramref name="reach"/>, each at a fixed angle so one more appears as the count goes up. The thaw's
	/// ice block draws its cracks with this, one per fifth of its heat.
	/// </summary>
	public static void DrawCracks(CanvasItem canvas, Vector2 centre, float reach, int count, Color color, float width = 1.5f)
	{
		for (int i = 0; i < count; i++)
		{
			float a = i * 1.3f + 0.4f;
			Vector2 from = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f;
			Vector2 to = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * reach;
			canvas.DrawLine(from, to, color, width);
		}
	}
}
