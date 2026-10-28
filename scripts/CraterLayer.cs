using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// One node per arena that draws every meteor crater: the hole (CraterGround) and the fire, smoke and
/// embers standing in it (CraterFire), on the ground, under the actors.
/// </summary>
/// <remarks>
/// A crater outlives the meteor that made it - the meteor is done once its debris has landed and its
/// burn has ticked out, and the crater then cools and dissolves for a second or two more - so the
/// craters live here, as a slot each in a fixed array (ImpactDebrisLayer's pattern), not in the
/// meteors' nodes. A volley of Orbital Bombardment is sixteen craters every two seconds; nothing is
/// instantiated or allocated per crater, and the oldest slot is reused when every one is live. The
/// node stops processing when nothing is live.
///
/// Two canvases, both under every actor: this node at absolute z -15 draws the holes in one batch (they
/// share CraterGround.Material), and its child at -14 draws the fire in them. Neither takes the
/// meteor's evolution tint as a modulate - a tint would multiply into the crater shader's numbers and
/// flatten the fire's white-hot core - so the fire is handed a softened tint and the ground none.
/// </remarks>
public partial class CraterLayer : Node2D
{
	private const int MaxCraters = 40;
	// A plain impact's crater: cooled in a second and a third, gone a second after that.
	private const float CoolSeconds = 1.3f;
	private const float LingerSeconds = 1.0f;
	private const float DissolveSeconds = 0.8f;
	// How much of the spell's tint reaches the fire: enough to say which evolution, not so much that a
	// red multiply drowns the white-hot heart (Magma Craters' 1.0, 0.35, 0.1 made every flame flat red).
	private const float FireTintShare = 0.3f;

	private struct Crater
	{
		public Vector2 At;
		public float Radius;
		public float Age;
		public float Burn;
		public float Life;
		public uint Seed;
		public Color Tint;
		public bool Live;
	}

	private readonly Crater[] craters = new Crater[MaxCraters];
	// Live slots ordered north to south each frame, so a nearer crater's fire stands in front.
	private readonly int[] order = new int[MaxCraters];
	private int orderCount;
	private int next;
	private int live;
	private uint seedClock;
	private Node2D fire;

	private static CraterLayer current;

	/// <summary>
	/// Opens a crater at <paramref name="globalAt"/> whose ejecta stops short of <paramref name="radius"/>
	/// (the radius the hit tested), on the layer of <paramref name="arena"/> (created there the first
	/// time). <paramref name="burnSeconds"/> above zero keeps it molten and filled with fire that long.
	/// </summary>
	public static void Open(Node2D arena, Vector2 globalAt, float radius, float burnSeconds, Color tint)
	{
		if (arena == null || !IsInstanceValid(arena))
			return;
		if (current == null || !IsInstanceValid(current) || !current.IsInsideTree() || current.GetParent() != arena)
		{
			current = new CraterLayer { Name = "Craters" };
			arena.AddChild(current);
		}
		current.Add(globalAt, radius, burnSeconds, tint);
	}

	public override void _Ready()
	{
		// On the ground: over the terrain and the walkable decals, under every actor (CraterGround).
		ZIndex = CraterGround.GroundZ;
		ZAsRelative = false;
		Material = CraterGround.Material;
		fire = new Node2D { Name = "Fire", ZIndex = CraterGround.FireZ, ZAsRelative = false };
		AddChild(fire);
		fire.Draw += DrawFire;
		SetProcess(false);
	}

	public override void _ExitTree()
	{
		if (fire != null && IsInstanceValid(fire))
			fire.Draw -= DrawFire;
		if (current == this)
			current = null;
	}

	private void Add(Vector2 globalAt, float radius, float burnSeconds, Color tint)
	{
		if (!craters[next].Live)
			live++;
		float burn = Mathf.Max(0f, burnSeconds);
		craters[next] = new Crater
		{
			At = globalAt,
			Radius = radius,
			Burn = burn,
			Life = burn + CoolSeconds + LingerSeconds,
			Seed = ++seedClock * 2654435761u,
			Tint = Colors.White.Lerp(tint, FireTintShare),
			Live = true,
		};
		next = (next + 1) % MaxCraters;
		SetProcess(true);
		QueueRedraw();
		fire.QueueRedraw();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		orderCount = 0;
		for (int i = 0; i < MaxCraters; i++)
		{
			if (!craters[i].Live)
				continue;
			craters[i].Age += dt;
			if (craters[i].Age >= craters[i].Life)
			{
				craters[i].Live = false;
				live--;
				continue;
			}
			// Insertion by y: at most forty, and nearly sorted from the frame before.
			int k = orderCount++;
			while (k > 0 && craters[order[k - 1]].At.Y > craters[i].At.Y)
			{
				order[k] = order[k - 1];
				k--;
			}
			order[k] = i;
		}
		QueueRedraw();
		fire.QueueRedraw();
		if (live <= 0)
		{
			live = 0;
			SetProcess(false);
		}
	}

	public override void _Draw()
	{
		for (int n = 0; n < orderCount; n++)
		{
			Crater c = craters[order[n]];
			if (!c.Live)
				continue;
			float heat = CraterGround.HeatAt(c.Age, c.Burn, CoolSeconds, c.Seed);
			float left = Mathf.Clamp((c.Life - c.Age) / DissolveSeconds, 0f, 1f);
			CraterGround.Draw(this, ToLocal(c.At), c.Radius, heat, c.Age, left);
		}
	}

	// Every crater's flames, then every crater's embers: two batches, not two per crater.
	private void DrawFire()
	{
		for (int n = 0; n < orderCount; n++)
		{
			Crater c = craters[order[n]];
			if (c.Live)
				CraterFire.DrawFire(fire, fire.ToLocal(c.At), c.Radius, c.Age, c.Burn, c.Seed, c.Tint);
		}
		for (int n = 0; n < orderCount; n++)
		{
			Crater c = craters[order[n]];
			if (c.Live)
				CraterFire.DrawEmbers(fire, fire.ToLocal(c.At), c.Radius, c.Age, c.Burn, c.Seed, c.Tint);
		}
	}
}
