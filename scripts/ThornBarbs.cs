using Godot;
using System;

namespace WizardSurvivors.scripts;

// Thorned Vine's Barbed Lash (ThornVine.BarbedLashId, 2026-10-29): the thorns break off in what the vine
// cuts, and a barbed enemy tears itself open as it walks on.
//
// The proposal said "the lashed bleed", and the game has no bleed - the vine already poisons, and a
// second damage-over-time would be a number nobody could see. What makes this one a different thing is
// what drives it: not a clock but a STRIDE. Every TearDistance an enemy walks from where the barb last
// tore, it tears again - a hit for TearShare of the lash, and a splash of sap left on the ground where it
// happened. A crowd walking at the wizard leaves a trail of sap behind it and pays for every step; one
// pinned in place (Entangle, Gravewell, a wall) pays nothing, which is the build decision.
//
// Measured as straight-line distance from the last tear, never summed frame to frame: a body pressed
// into a crowd jitters a pixel or two every frame under the separation push, and a summed path would
// tear it open standing still.
//
// One node per arena, made on the first barb (For). It draws the barbs on the bodies in the air layer,
// over them, and the sap on the ground under every body through a child at DrawLayers.Ground. Fixed
// arrays; nothing allocated per frame.
public partial class ThornBarbs : Node2D
{
	/// <summary>How far a barbed enemy walks between tears.</summary>
	public const float TearDistance = 48f;
	/// <summary>A tear's hit, as a share of the lash that left the barb.</summary>
	public const float TearShare = 0.35f;
	/// <summary>How long a barb stays in; a second lash re-sets it.</summary>
	public const float BarbSeconds = 4f;

	private const int MaxBarbs = 96;
	private const int MaxDrops = 128;
	private const float DropSeconds = 1.4f;

	private static ThornBarbs live;

	private struct Lodged
	{
		public Enemy Enemy;
		public Vector2 Anchor;
		public float Until;
		public int Damage;
		public SpellData Source;
		public Player Player;
		public float Seed;
	}

	private struct Drop
	{
		public Vector2 At;
		public float Born;
		public float Seed;
	}

	private readonly Lodged[] barbs = new Lodged[MaxBarbs];
	private readonly Drop[] drops = new Drop[MaxDrops];
	private int dropNext;
	private int liveBarbs;
	private float clock;
	private Node2D sap;
	private bool drewDrops;

	private static readonly Color Occ = new Color(0.020f, 0.027f, 0.047f, 1f);
	private static readonly Color Thorn = new Color(0.812f, 0.933f, 0.580f, 1f);      // grass hot #CFEE94
	private static readonly Color ThornShade = new Color(0.290f, 0.549f, 0.227f, 1f); // grass edge #4A8C3A
	private static readonly Color SapHot = new Color(0.863f, 0.957f, 0.549f, 1f);     // poison hot #DCF48C
	private static readonly Color SapMid = new Color(0.706f, 0.878f, 0.290f, 1f);     // poison mid #B4E04A
	private static readonly Color SapEdge = new Color(0.420f, 0.580f, 0.125f, 1f);    // poison edge #6B9420

	/// <summary>The arena's barbs, made on first use.</summary>
	public static ThornBarbs For(Node2D arena)
	{
		if (live != null && IsInstanceValid(live) && live.GetParent() == arena)
			return live;
		live = new ThornBarbs();
		arena.AddChild(live);
		return live;
	}

	public override void _Ready()
	{
		Name = "ThornBarbs";
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		sap = new Node2D { Name = "Sap", ZAsRelative = false, ZIndex = DrawLayers.Ground };
		AddChild(sap);
		sap.Draw += DrawSap;
	}

	public override void _ExitTree()
	{
		if (live == this)
			live = null;
		if (sap != null && IsInstanceValid(sap))
			sap.Draw -= DrawSap;
	}

	/// <summary>
	/// Lodges a barb in <paramref name="enemy"/>, cut by a lash of <paramref name="lashDamage"/>. A
	/// second barb in the same enemy re-sets the first (the stronger lash and the later end win).
	/// </summary>
	public void Barb(Enemy enemy, int lashDamage, SpellData source, Player player)
	{
		if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying)
			return;
		int free = -1;
		int oldest = 0;
		for (int i = 0; i < MaxBarbs; i++)
		{
			if (ReferenceEquals(barbs[i].Enemy, enemy))
			{
				barbs[i].Until = clock + BarbSeconds;
				barbs[i].Damage = Math.Max(barbs[i].Damage, lashDamage);
				return;
			}
			if (free < 0 && barbs[i].Enemy == null)
				free = i;
			if (barbs[i].Until < barbs[oldest].Until)
				oldest = i;
		}
		int slot = free >= 0 ? free : oldest;
		if (free >= 0)
			liveBarbs++;
		barbs[slot] = new Lodged
		{
			Enemy = enemy,
			Anchor = enemy.GlobalPosition,
			Until = clock + BarbSeconds,
			Damage = lashDamage,
			Source = source,
			Player = player,
			Seed = (enemy.GetHashCode() & 255) / 256f * Mathf.Tau,
		};
	}

	public override void _Process(double delta)
	{
		clock += (float)delta;
		if (liveBarbs > 0)
		{
			float tear2 = TearDistance * TearDistance;
			for (int i = 0; i < MaxBarbs; i++)
			{
				Enemy enemy = barbs[i].Enemy;
				if (enemy == null)
					continue;
				if (!IsInstanceValid(enemy) || enemy.IsDying || clock >= barbs[i].Until)
				{
					barbs[i] = default;
					liveBarbs--;
					continue;
				}
				Vector2 at = enemy.GlobalPosition;
				if (at.DistanceSquaredTo(barbs[i].Anchor) < tear2)
					continue;
				barbs[i].Anchor = at;
				AddDrop(at + new Vector2(0f, enemy.StatusFxGeometry.FeetY - 2f), barbs[i].Seed + clock);
				Player player = barbs[i].Player;
				if (player != null && IsInstanceValid(player))
					player.DealDamageToEnemy(enemy, Math.Max(1, Mathf.RoundToInt(barbs[i].Damage * TearShare)), source: barbs[i].Source);
			}
			QueueRedraw();
		}

		bool anyDrops = false;
		for (int i = 0; i < MaxDrops && !anyDrops; i++)
			anyDrops = drops[i].Born > 0f && clock - drops[i].Born < DropSeconds;
		if (anyDrops || drewDrops)
			sap.QueueRedraw();
		drewDrops = anyDrops;
	}

	private void AddDrop(Vector2 at, float seed)
	{
		drops[dropNext] = new Drop { At = at, Born = MathF.Max(0.0001f, clock), Seed = seed };
		dropNext = (dropNext + 1) % MaxDrops;
	}

	// A thorn broken off in the body, two or three of them, raked the way the lash went: pale with a
	// dark outline so it reads on any skin.
	public override void _Draw()
	{
		float g = WhipLimb.Grid;
		for (int i = 0; i < MaxBarbs; i++)
		{
			Enemy enemy = barbs[i].Enemy;
			if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			StatusFxGeometry body = enemy.StatusFxGeometry;
			Vector2 centre = ToLocal(enemy.GlobalPosition) + new Vector2(0f, (body.FeetY + body.HeadY) * 0.5f);
			float w = MathF.Max(6f, body.HalfWidth * 0.55f);
			for (int t = 0; t < 2; t++)
			{
				float a = barbs[i].Seed + t * 2.6f;
				Vector2 at = centre + new Vector2(MathF.Cos(a) * w, MathF.Sin(a) * w * 0.7f);
				// The thorn slants up and out from where it went in: three art pixels, dark-rooted and
				// pale at the point, in an outline so it holds over any skin and any status tint.
				Vector2 tipDir = new Vector2(MathF.Cos(a) >= 0f ? 1f : -1f, -1f);
				for (int k = 0; k < 3; k++)
					DrawRect(WhipLimb.Cell(at + tipDir * g * k, g * 2f), Occ);
				DrawRect(WhipLimb.Cell(at, g), ThornShade);
				DrawRect(WhipLimb.Cell(at + tipDir * g, g), Thorn);
				DrawRect(WhipLimb.Cell(at + tipDir * g * 2f, g), SapHot);
			}
		}
	}

	// Sap where a barb tore: a splash of poison-green cells that darkens and is eaten away, dithered.
	private void DrawSap()
	{
		float g = WhipLimb.Grid;
		for (int i = 0; i < MaxDrops; i++)
		{
			if (drops[i].Born <= 0f)
				continue;
			float since = clock - drops[i].Born;
			if (since >= DropSeconds)
				continue;
			float u = since / DropSeconds;
			Vector2 at = sap.ToLocal(drops[i].At);
			float s = drops[i].Seed;
			Color core = u < 0.25f ? SapHot : u < 0.6f ? SapMid : SapEdge;
			// The splash spreads in its first instant, then is eaten away cell by cell from its rim.
			float spread = g * (1f + 1.5f * Mathf.Clamp(u * 6f, 0f, 1f));
			for (int k = 0; k < 6; k++)
			{
				float h = Mathf.PosMod(MathF.Sin(s * 12.9898f + k * 78.233f) * 43758.5453f, 1f);
				if (h < u * 1.1f)
					continue;
				float a = s * 3f + k * 1.05f;
				float r = k == 0 ? 0f : spread * (0.6f + 0.6f * h);
				DrawRect(WhipLimb.Cell(at + new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r * 0.6f), k == 0 ? g * 2f : g), k == 0 ? SapEdge : core);
			}
		}
	}
}
