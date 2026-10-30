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
// Drawn in the air over every body (DrawLayers.Air) - a gas hangs over what is in it - as billowing lobes
// of the Poison ramp on the world's 2 px art grid, dithered so the bodies in it still show, and thinning
// away by that dither (GasCloud, spore_cloud.gdshader, 2026-10-29: it was a scatter of 4 px squares - two
// art pixels each, coarser than everything round it - picked at random). Never a translucent disc. One
// node per arena, made on the first breath (For); a fixed ring of clouds; one quad a cloud, all in one
// batch; nothing allocated per frame.
public partial class FenGas : Node2D
{
	/// <summary>Seconds between one breath of a pool and its next (the trail's breaths are spread over its pools).</summary>
	public const float Interval = 1.6f;
	public const float Seconds = 2.2f;
	public const float Radius = 32f;
	public const float PoisonSeconds = 3f;
	private const float TickSeconds = 0.5f;
	private const int MaxClouds = 32;

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

	// The Poison ramp's clouds (spore_cloud.gdshader through GasCloud).
	private ShaderMaterial material;

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
		material = GasCloud.MakeMaterial(ElementColors.GetRamp(Element.Poison));
		Material = material;
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
		{
			GasCloud.SetTime(material, clock);
			QueueRedraw();
		}
		drew = any;
	}

	// Swells out of the water to its full size, and holds it until it thins away.
	private static float RadiusAt(float age) => Radius * (0.45f + 0.55f * Mathf.Clamp(age / 0.35f, 0f, 1f));

	// One cloud a breath: it swells out of the water, hangs, and thins away cell by cell in its last
	// 0.6 s. Its lobes' outer edges are RadiusAt, the reach its tick poisons.
	public override void _Draw()
	{
		for (int i = 0; i < MaxClouds; i++)
		{
			if (!clouds[i].Live)
				continue;
			float age = clock - clouds[i].Born;
			float thin = Mathf.Clamp((age - (Seconds - 0.6f)) / 0.6f, 0f, 1f);
			GasCloud.Draw(this, ToLocal(clouds[i].At), RadiusAt(age), thin, clouds[i].Seed * 0.01f, 0.7f);
		}
	}
}
