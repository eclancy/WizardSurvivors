using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Mirefoot's Fen Gas (TrailWeaver.FenGasId, 2026-10-29): the bog breathes. Every pool of the trail lets a
// cloud of marsh gas up out of the water now and then, and the cloud poisons whatever it hangs over.
//
// A cloud rises out of a point inside its pool, swells to Radius in its first moments, drifts a little
// upward, hangs for Seconds and thins away. Every TickSeconds it poisons each enemy under it (Enemy
// .ApplyPoison: the strongest dose and the longest time win, so overlapping clouds never stack a dose).
// THE RADIUS IS THE DOSE: the cloud is drawn out to the radius it tests and no further.
//
// Drawn in the air over every body (DrawLayers.Air) - a gas hangs over what is in it - as dithered cells
// of the Poison ramp on the 2 px art grid, never a translucent disc. One node per arena, made on the first
// breath (For); a fixed ring of clouds; nothing allocated per frame.
public partial class FenGas : Node2D
{
	/// <summary>Seconds between one breath of a pool and its next (the trail's breaths are spread over its pools).</summary>
	public const float Interval = 1.6f;
	public const float Seconds = 2.2f;
	public const float Radius = 32f;
	public const float PoisonSeconds = 3f;
	private const float TickSeconds = 0.5f;
	private const int MaxClouds = 32;
	private const float Cell = 4f;

	private static FenGas live;
	private static readonly List<Enemy> scratch = new();

	private struct Cloud
	{
		public Vector2 At;
		public float Born;
		public float NextTick;
		public int Poison;
		public float Seed;
		public bool Live;
	}

	private readonly Cloud[] clouds = new Cloud[MaxClouds];
	private int next;
	private float clock;
	private bool drew;

	private static readonly Color Occ = new Color(0.020f, 0.027f, 0.047f, 1f);
	private static readonly Color Hot = new Color(0.863f, 0.957f, 0.549f, 1f);  // poison hot #DCF48C
	private static readonly Color Mid = new Color(0.706f, 0.878f, 0.290f, 1f);  // poison mid #B4E04A
	private static readonly Color Edge = new Color(0.420f, 0.580f, 0.125f, 1f); // poison edge #6B9420

	public static FenGas For(Node2D arena)
	{
		if (live != null && IsInstanceValid(live) && live.GetParent() == arena)
			return live;
		live = new FenGas();
		arena.AddChild(live);
		return live;
	}

	public override void _Ready()
	{
		Name = "FenGas";
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
	}

	public override void _ExitTree()
	{
		if (live == this)
			live = null;
	}

	/// <summary>A cloud rises at <paramref name="at"/> (world), poisoning for <paramref name="poison"/> a tick.</summary>
	public void Breathe(Vector2 at, int poison)
	{
		clouds[next] = new Cloud
		{
			At = at,
			Born = clock,
			NextTick = clock + 0.15f,
			Poison = Math.Max(1, poison),
			Seed = (next * 0.618034f + clock) % 1f * 97f,
			Live = true,
		};
		next = (next + 1) % MaxClouds;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		bool any = false;
		for (int i = 0; i < MaxClouds; i++)
		{
			if (!clouds[i].Live)
				continue;
			if (clock - clouds[i].Born >= Seconds)
			{
				clouds[i].Live = false;
				continue;
			}
			any = true;
			clouds[i].At += new Vector2(0f, -6f * dt);
			if (clock < clouds[i].NextTick)
				continue;
			clouds[i].NextTick = clock + TickSeconds;
			float r = RadiusAt(clock - clouds[i].Born);
			float r2 = r * r;
			Vector2 at = clouds[i].At;
			EnemyRegistry.CollectNear(at, r, scratch);
			foreach (Enemy enemy in scratch)
			{
				if (IsInstanceValid(enemy) && !enemy.IsDying && at.DistanceSquaredTo(enemy.GlobalPosition) <= r2)
					enemy.ApplyPoison(clouds[i].Poison, PoisonSeconds);
			}
			scratch.Clear();
		}
		if (any || drew)
			QueueRedraw();
		drew = any;
	}

	// Swells out of the water to its full size, and holds it until it thins away.
	private static float RadiusAt(float age) => Radius * (0.45f + 0.55f * Mathf.Clamp(age / 0.35f, 0f, 1f));

	public override void _Draw()
	{
		for (int i = 0; i < MaxClouds; i++)
		{
			if (!clouds[i].Live)
				continue;
			float age = clock - clouds[i].Born;
			float r = RadiusAt(age);
			float thin = Mathf.Clamp((age - (Seconds - 0.6f)) / 0.6f, 0f, 1f);
			Vector2 c = ToLocal(clouds[i].At);
			float s = clouds[i].Seed;
			int n = (int)MathF.Ceiling(r / Cell);
			for (int y = -n; y <= n; y++)
			{
				for (int x = -n; x <= n; x++)
				{
					Vector2 o = new Vector2(x, y) * Cell;
					// Three lobes, so a cloud is a puff rather than a disc.
					float d = o.Length() / r;
					float lobe = 0.12f * MathF.Sin(MathF.Atan2(o.Y, o.X) * 3f + s + age * 1.5f);
					if (d > 0.92f + lobe)
						continue;
					float h = Mathf.PosMod(MathF.Sin(x * 12.9898f + y * 78.233f + s) * 43758.5453f, 1f);
					// Sparse at the edge, denser in the middle; thinning eats it cell by cell.
					if (h > 0.75f - 0.45f * d || h < thin)
						continue;
					Color tone = d < 0.35f && h > 0.5f ? Hot : (y < 0 ? Mid : Edge);
					Vector2 at = new Vector2(MathF.Round((c.X + o.X) / 2f) * 2f, MathF.Round((c.Y + o.Y) / 2f) * 2f);
					DrawRect(new Rect2(at.X, at.Y, Cell, Cell), tone);
				}
			}
			// The bubble it rose out of, in its first instant.
			if (age < 0.2f)
				DrawRect(WhipLimb.Cell(c, Cell * 1.5f), Occ);
		}
	}
}
