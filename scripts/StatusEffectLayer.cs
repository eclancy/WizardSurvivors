using Godot;
using System.Collections.Generic;

/// <summary>
/// The debuffs an enemy can be seen wearing. Bit order is not the draw order - see
/// <see cref="StatusEffectLayer"/> for that - but each flag's row in the atlas is fixed.
/// </summary>
[System.Flags]
public enum StatusVisual
{
	None = 0,
	Slow = 1 << 0,
	Freeze = 1 << 1,
	Poison = 1 << 2,
	Vulnerable = 1 << 3,
	Weaken = 1 << 4,
	Wither = 1 << 5,
}

/// <summary>
/// Where on an enemy a status mark is drawn, as an enemy measured once from its own sprite
/// (<see cref="Enemy.StatusFxGeometry"/>). Local to the enemy, before the enemy's own scale.
/// </summary>
public readonly struct StatusFxGeometry
{
	public readonly float FeetY;
	public readonly float HeadY;
	public readonly float HalfWidth;
	// World units per atlas pixel: 2 for anything on the basic or elite cell, 4 on a boss cell.
	public readonly float PixelScale;

	public StatusFxGeometry(float feetY, float headY, float halfWidth, float pixelScale)
	{
		FeetY = feetY;
		HeadY = headY;
		HalfWidth = halfWidth;
		PixelScale = pixelScale;
	}
}

/// <summary>
/// Draws every enemy's status marks - slow, freeze, poison, vulnerable, weaken, wither - in two
/// passes for the whole arena: one under the swarm (the freeze crust on the ground at an enemy's
/// feet) and one over it (everything else, on the body, at the feet and over the head).
/// </summary>
/// <remarks>
/// <para><b>Why one shared drawer rather than a node per enemy.</b> The phone runs the
/// Compatibility renderer with up to 140 enemies on screen, and every enemy is its own sprite
/// sheet, so the swarm is already one draw call per body. A child sprite per enemy per status
/// would add a texture switch to every one of them. Here every mark comes out of one atlas
/// (<c>assets/bonelight/effects/status-marks.png</c>, drawn by <c>tools/art/status_fx.py</c>) into
/// one of two canvas items, so a fully debuffed swarm is two batches. No nodes are created per
/// enemy, nothing is allocated per frame, and an arena with nothing debuffed costs one empty list
/// check.</para>
///
/// <para><b>Who is drawn.</b> An enemy registers itself (<see cref="Track"/>) when a debuff lands on
/// it, and is dropped the frame its last debuff ends, the frame it starts dying
/// (<c>Enemy.StartDeath</c> unregisters it outright, in the same frame it leaves the group), when it
/// is recycled (<c>ResetForRespawn</c>), or when it leaves the tree. A burrowed boss
/// (<c>SetTargetable(false)</c>) keeps its debuffs but is not drawn.</para>
///
/// <para><b>Two marks per enemy, at most.</b> A fully debuffed swarm with six marks on every
/// body would be noise, so each enemy shows the first two of its debuffs in
/// <see cref="Priority"/> order: the ones that change what the player should DO first (a frozen
/// or slowed enemy is one they can walk past; a vulnerable one is the one to hit), the ones that
/// only change a number last. Slow and freeze also tint the sprite and slow or stop its animation
/// (<c>Enemy.RefreshStatusLook</c>), which does not count against the two, so a slowed enemy still
/// reads as slowed when its mark has been outranked.</para>
///
/// <para>Each mark has its own place on the body - feet, over the head, either shoulder, the
/// torso - so the two shown never sit on top of each other. The last 0.3 s of a timed debuff
/// fades out, which is the tell that it is about to wear off.</para>
/// </remarks>
public partial class StatusEffectLayer : Node2D
{
	/// <summary>The layer in the running arena, or null. Set on entering the tree, cleared on leaving it.</summary>
	public static StatusEffectLayer? Current { get; private set; }

	/// <summary>
	/// Off for a cost A/B (_BalanceHarness <c>--no-status-fx</c>): no marks drawn, no tints, no
	/// animation slowing, and nothing registered - the debuffs themselves are untouched.
	/// </summary>
	public static bool Disabled { get; set; }

	// Cost of the layer itself, read by the balance harness: Stopwatch ticks spent pruning and
	// drawing, the frames they were spent over, and how many marks were drawn.
	public static bool MeasureCost { get; set; }
	public static long PerfTicks { get; private set; }
	// The share of PerfTicks spent issuing draw commands, as against reading the enemies.
	public static long PerfDrawTicks { get; private set; }
	public static long PerfFrames { get; private set; }
	public static long PerfMarks { get; private set; }
	public static void ResetPerf() { PerfTicks = 0; PerfDrawTicks = 0; PerfFrames = 0; PerfMarks = 0; }

	public const string AtlasPath = "res://assets/bonelight/effects/status-marks.png";
	public const int Cell = 24;
	public const int Frames = 6;
	public const int Rows = 6;
	public const int MaxMarksPerEnemy = 2;

	// Above the ground AoEs (several still sit at 1-2; the pools and craters lie on the ground at -15),
	// so a mark stays readable inside the cloud that applied it; below the player's own effects
	// (FlamePuff 5, KindledWard 6).
	private const int OverZIndex = 3;
	// Under every body (0) and the elite ring (-1), over the walkable decor (-20).
	private const int UnderZIndex = -2;
	private const float FadeSeconds = 0.3f;

	private enum Anchor { Feet, AboveHead, RightShoulder, LeftShoulder, Torso }

	private readonly struct Mark
	{
		public readonly StatusVisual Status;
		public readonly int Row;
		public readonly float Fps;
		// The atlas pixel, inside the 24x24 cell, that sits on the anchor.
		public readonly float PivotX;
		public readonly float PivotY;
		public readonly Anchor Anchor;
		public readonly bool Under;

		public Mark(StatusVisual status, int row, float fps, float pivotX, float pivotY, Anchor anchor, bool under)
		{
			Status = status;
			Row = row;
			Fps = fps;
			PivotX = pivotX;
			PivotY = pivotY;
			Anchor = anchor;
			Under = under;
		}
	}

	// Draw priority, and the whole table. Rows are tools/art/status_fx.py's ROWS order. Frame rates
	// are chosen per mark: slow is deliberately the most sluggish thing on screen.
	private static readonly Mark[] Priority =
	{
		new(StatusVisual.Freeze, 1, 8f, 11.5f, 20f, Anchor.Feet, true),
		// Over the body, not under it like freeze: under, a cloaked enemy hid nearly all of it.
		new(StatusVisual.Slow, 0, 5f, 12f, 20f, Anchor.Feet, false),
		new(StatusVisual.Vulnerable, 3, 6f, 12f, 22f, Anchor.AboveHead, false),
		new(StatusVisual.Poison, 2, 8f, 12f, 21f, Anchor.RightShoulder, false),
		new(StatusVisual.Weaken, 4, 8f, 12f, 10f, Anchor.LeftShoulder, false),
		new(StatusVisual.Wither, 5, 8f, 12f, 11f, Anchor.Torso, false),
	};

	// Sized once for a full arena plus the elites and side-event bodies that can sit over the cap,
	// so a swarm debuffing does not grow the list mid-fight.
	private readonly List<Enemy> tracked = new(256);
	private Texture2D? atlas;
	private Node2D? under;
	private float clock;
	private bool drewLastFrame;

	public override void _EnterTree()
	{
		Current = this;
	}

	public override void _ExitTree()
	{
		if (Current == this)
			Current = null;
		tracked.Clear();
	}

	public override void _Ready()
	{
		Name = "StatusEffectLayer";
		ZIndex = OverZIndex;
		atlas = GD.Load<Texture2D>(AtlasPath);
		if (atlas == null)
			GD.PrintErr($"StatusEffectLayer: {AtlasPath} did not load - run python tools/art/status_fx.py and import it.");

		under = new Node2D
		{
			Name = "Under",
			ZIndex = UnderZIndex,
			// Absolute, not relative to this node's 3, or the ground marks would draw over the swarm.
			ZAsRelative = false,
		};
		AddChild(under);
		under.Draw += DrawUnder;
	}

	/// <summary>Starts drawing this enemy's marks. Cheap to call on every debuff application.</summary>
	public void Track(Enemy enemy)
	{
		if (Disabled || enemy.StatusFxSlot >= 0)
			return;
		enemy.StatusFxSlot = tracked.Count;
		tracked.Add(enemy);
	}

	/// <summary>Stops drawing this enemy's marks at once - on death, on recycling, on leaving the tree.</summary>
	public void Untrack(Enemy enemy)
	{
		int slot = enemy.StatusFxSlot;
		enemy.StatusFxSlot = -1;
		if (slot < 0 || slot >= tracked.Count || tracked[slot] != enemy)
			return;
		RemoveAt(slot);
	}

	// Swap-remove, so dropping one enemy never shifts the rest of the list.
	private void RemoveAt(int slot)
	{
		int last = tracked.Count - 1;
		if (slot != last)
		{
			Enemy moved = tracked[last];
			tracked[slot] = moved;
			if (IsInstanceValid(moved))
				moved.StatusFxSlot = slot;
		}
		tracked.RemoveAt(last);
	}

	public override void _Process(double delta)
	{
		if (Disabled)
			return;

		long start = MeasureCost ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;

		// Wrapped so the frame index never loses precision on a long run. Every mark's rate times
		// 60 is a multiple of six frames, so the wrap lands on frame zero and nothing hitches.
		clock = (clock + (float)delta) % 60f;

		overDraws.Clear();
		underDraws.Clear();
		// Both canvas items share this node's transform (Under has no offset of its own).
		Transform2D toLocal = GetGlobalTransform().AffineInverse();

		// Every enemy is read here once a frame and both draw passes only replay the result. Each
		// Godot property read from C# is a call across the interop boundary, and the first version,
		// which read position and scale per mark per pass, measured nearly three times the cost.
		// IsInstanceValid is managed-side only; a freed enemy has already left the tree, and
		// _ExitTree untracks it, so this is belt and braces rather than the real guard.
		for (int i = tracked.Count - 1; i >= 0; i--)
		{
			Enemy e = tracked[i];
			StatusVisual active = IsInstanceValid(e) && !e.IsDying ? e.ActiveStatusVisuals : StatusVisual.None;
			if (active == StatusVisual.None)
			{
				if (IsInstanceValid(e))
					e.StatusFxSlot = -1;
				RemoveAt(i);
				continue;
			}
			// Burrowed: still debuffed, not drawn.
			if (!e.IsTargetable)
				continue;
			Collect(e, active, toLocal);
		}

		// One more redraw after the last mark goes, so it does not linger on screen.
		bool drawing = overDraws.Count > 0 || underDraws.Count > 0;
		if (drawing || drewLastFrame)
		{
			QueueRedraw();
			under?.QueueRedraw();
		}
		drewLastFrame = drawing;

		if (MeasureCost)
		{
			PerfTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
			PerfFrames++;
			PerfMarks += overDraws.Count + underDraws.Count;
		}
	}

	private readonly struct MarkDraw
	{
		public readonly Rect2 Dest;
		public readonly Rect2 Source;
		public readonly float Alpha;

		public MarkDraw(Rect2 dest, Rect2 source, float alpha)
		{
			Dest = dest;
			Source = source;
			Alpha = alpha;
		}
	}

	// Rebuilt every frame and never shrunk, so after the first full swarm nothing here allocates.
	private readonly List<MarkDraw> overDraws = new(512);
	private readonly List<MarkDraw> underDraws = new(256);

	private void Collect(Enemy e, StatusVisual active, Transform2D toLocal)
	{
		// One read for position and scale together. The enemy's scale carries elites
		// (EliteScaleMultiplier) and the hit-flash pop; no enemy rotates.
		Transform2D xf = e.GlobalTransform;
		float scale = xf.X.Length();
		StatusFxGeometry geo = e.StatusFxGeometry;
		float body = geo.FeetY - geo.HeadY;
		float px = geo.PixelScale * scale;
		int phase = e.StatusFxPhase;

		int shown = 0;
		for (int m = 0; m < Priority.Length && shown < MaxMarksPerEnemy; m++)
		{
			Mark mark = Priority[m];
			if ((active & mark.Status) == 0)
				continue;
			shown++;

			Vector2 anchor = mark.Anchor switch
			{
				Anchor.Feet => new Vector2(0f, geo.FeetY),
				Anchor.AboveHead => new Vector2(0f, geo.HeadY),
				Anchor.RightShoulder => new Vector2(geo.HalfWidth * 0.7f, geo.HeadY + body * 0.32f),
				Anchor.LeftShoulder => new Vector2(-geo.HalfWidth * 0.75f, geo.HeadY + body * 0.18f),
				_ => new Vector2(0f, geo.HeadY + body * 0.42f),
			};
			Vector2 at = toLocal * (xf.Origin + anchor * scale);

			int frame = ((int)(clock * mark.Fps) + phase) % Frames;
			var source = new Rect2(frame * Cell, mark.Row * Cell, Cell, Cell);
			var dest = new Rect2(at.X - mark.PivotX * px, at.Y - mark.PivotY * px, Cell * px, Cell * px);

			float remaining = e.StatusVisualTimeRemaining(mark.Status);
			float alpha = remaining >= FadeSeconds ? 1f : Mathf.Clamp(remaining / FadeSeconds, 0f, 1f);
			(mark.Under ? underDraws : overDraws).Add(new MarkDraw(dest, source, alpha));
		}
	}

	public override void _Draw() => Replay(this, overDraws);

	private void DrawUnder()
	{
		if (under != null)
			Replay(under, underDraws);
	}

	private void Replay(CanvasItem target, List<MarkDraw> draws)
	{
		if (atlas == null || draws.Count == 0)
			return;
		long start = MeasureCost ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
		for (int i = 0; i < draws.Count; i++)
		{
			MarkDraw d = draws[i];
			target.DrawTextureRectRegion(atlas, d.Dest, d.Source, new Color(1f, 1f, 1f, d.Alpha));
		}
		if (MeasureCost)
		{
			long spent = System.Diagnostics.Stopwatch.GetTimestamp() - start;
			PerfTicks += spent;
			PerfDrawTicks += spent;
		}
	}
}
