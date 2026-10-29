using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// A ring of splinters thrown out of something that breaks: Glacial Spike's Shatter (ice, as the spike
/// withdraws) and Gravewell's Bone Spikes (bone, as the grave springs). Not a node: its owner ticks it
/// and draws it, in the air over the bodies, from its own <c>_Process</c> and <c>_Draw</c>.
/// </summary>
/// <remarks>
/// Each splinter starts at the rim of what threw it - what is inside was already hit - and flies straight
/// out to <c>range</c>, striking the first living enemy whose body it touches and breaking on it. A maze
/// wall stops it (water does not: a splinter flies over a tarn, as an enemy bolt does). One hit a
/// splinter, so the most a ring can deal is its count times its damage, and what it hits is exactly what
/// the eye saw it reach. The neighbour query is the enemy grid's (EnemyRegistry.ForEachNear) with a struct
/// visitor; nothing here allocates after construction.
/// </remarks>
public sealed class ThrownShards
{
	public const int MaxShards = 16;
	// A body is a body, not a point: a splinter strikes what it comes this close to.
	private const float HitTolerance = 13f;

	private readonly Vector2[] at = new Vector2[MaxShards];
	private readonly Vector2[] dir = new Vector2[MaxShards];
	private readonly float[] travelled = new float[MaxShards];
	private readonly bool[] live = new bool[MaxShards];
	private int count;
	private float speed;
	private float range;
	private int damage;
	private SpellData source;
	private Player player;

	/// <summary>True while any splinter is still in flight.</summary>
	public bool Active { get; private set; }

	/// <summary>
	/// Throws <paramref name="shards"/> splinters out of <paramref name="centre"/> (world), evenly round it
	/// from <paramref name="phase"/> radians, each starting at <paramref name="fromRadius"/> and flying
	/// <paramref name="flyRange"/> further at <paramref name="flySpeed"/>.
	/// </summary>
	public void Throw(Vector2 centre, int shards, float fromRadius, float flyRange, float flySpeed, float phase,
		int hitDamage, SpellData spell, Player owner)
	{
		count = Math.Clamp(shards, 0, MaxShards);
		speed = MathF.Max(1f, flySpeed);
		range = MathF.Max(0f, flyRange);
		damage = Math.Max(1, hitDamage);
		source = spell;
		player = owner;
		for (int i = 0; i < count; i++)
		{
			float a = phase + Mathf.Tau * i / count;
			dir[i] = new Vector2(MathF.Cos(a), MathF.Sin(a));
			at[i] = centre + dir[i] * fromRadius;
			travelled[i] = 0f;
			live[i] = true;
		}
		Active = count > 0;
	}

	/// <summary>Moves every splinter and strikes what it reaches.</summary>
	public void Update(float delta)
	{
		if (!Active)
			return;
		bool any = false;
		MazeNavigation nav = MazeNavigation.Active;
		var visitor = new FirstLiving();
		for (int i = 0; i < count; i++)
		{
			if (!live[i])
				continue;
			float step = speed * delta;
			at[i] += dir[i] * step;
			travelled[i] += step;
			if (travelled[i] >= range || (nav != null && nav.IsWallAt(at[i])))
			{
				live[i] = false;
				continue;
			}
			visitor.Found = null;
			EnemyRegistry.ForEachNear(at[i], HitTolerance, ref visitor);
			if (visitor.Found != null)
			{
				live[i] = false;
				if (player != null && GodotObject.IsInstanceValid(player))
					player.DealDamageToEnemy(visitor.Found, damage, source: source);
				continue;
			}
			any = true;
		}
		Active = any;
	}

	/// <summary>
	/// Draws every splinter in flight into <paramref name="canvas"/>, whose world-to-local transform is
	/// <paramref name="toLocal"/>: a splinter along its flight, <paramref name="tail"/> to
	/// <paramref name="tip"/>, in an occlusion outline so it holds over any body.
	/// </summary>
	public void Draw(CanvasItem canvas, Transform2D toLocal, Color tail, Color body, Color tip, Color outline)
	{
		if (!Active)
			return;
		float g = WhipLimb.Grid;
		for (int i = 0; i < count; i++)
		{
			if (!live[i])
				continue;
			Vector2 p = toLocal * at[i];
			Vector2 d = dir[i];
			// Four cells along its flight, two art pixels thick at the tail and one at the point.
			for (int k = 0; k < 4; k++)
				canvas.DrawRect(WhipLimb.Cell(p - d * g * 1.5f * k, k < 2 ? g * 3f : g * 4f), outline);
			canvas.DrawRect(WhipLimb.Cell(p - d * g * 4.5f, g * 2f), tail);
			canvas.DrawRect(WhipLimb.Cell(p - d * g * 3f, g * 2f), body);
			canvas.DrawRect(WhipLimb.Cell(p - d * g * 1.5f, g), body);
			canvas.DrawRect(WhipLimb.Cell(p, g), tip);
		}
	}

	// The first living body the grid finds near a point.
	private struct FirstLiving : INeighbourVisitor
	{
		public Enemy Found;

		public void Visit(Enemy enemy, Vector2 position)
		{
			if (Found == null && !enemy.IsDying)
				Found = enemy;
		}
	}
}
