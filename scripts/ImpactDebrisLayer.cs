using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// One node per arena that draws every debris burst that has no node of its own to draw in: Frost
/// Shard's shatter on every kill, and the reactive boons' burst when the player is struck.
/// </summary>
/// <remarks>
/// Both used to make a Line2D ring and a Tween per event and free them 0.2 s later - on a dense
/// screen Frost Shard made one per kill - and both rings grew past the radius they had hit as they
/// faded (x1.18 and x1.12), so the tell overstated its reach on the way out. Now a burst is a slot
/// in a fixed array, drawn by ImpactDebris out of the radius that was tested, and nothing is
/// instantiated or allocated per event. The node stops processing when nothing is live.
/// </remarks>
public partial class ImpactDebrisLayer : Node2D
{
	private const int MaxBursts = 48;

	private struct Burst
	{
		public Vector2 At;
		public float Radius;
		public float Age;
		public uint Seed;
		public ElementColors.Ramp Ramp;
		public Color Tint;
		public int Count;
		public bool Live;
	}

	private readonly Burst[] bursts = new Burst[MaxBursts];
	private int next;
	private int live;
	private uint seedClock;

	private static ImpactDebrisLayer current;

	/// <summary>
	/// A burst at <paramref name="globalAt"/> reaching exactly <paramref name="radius"/>, on the
	/// layer of the arena <paramref name="arena"/> is (created there the first time).
	/// </summary>
	public static void Spawn(Node2D arena, Vector2 globalAt, float radius, in ElementColors.Ramp ramp, Color tint)
	{
		if (arena == null || !IsInstanceValid(arena))
			return;
		if (current == null || !IsInstanceValid(current) || !current.IsInsideTree() || current.GetParent() != arena)
		{
			current = new ImpactDebrisLayer { Name = "ImpactDebris" };
			arena.AddChild(current);
		}
		current.Add(globalAt, radius, ramp, tint);
	}

	public override void _Ready()
	{
		// Over the bodies it bursts among, as the Line2D rings it replaced were; under the HUD.
		ZIndex = 6;
		SetProcess(false);
	}

	public override void _ExitTree()
	{
		if (current == this)
			current = null;
	}

	private void Add(Vector2 globalAt, float radius, in ElementColors.Ramp ramp, Color tint)
	{
		// The oldest slot is reused when every one is live: on a screen that busy one burst more or
		// less is not visible, and a cap is what keeps the cost flat.
		if (!bursts[next].Live)
			live++;
		bursts[next] = new Burst
		{
			At = globalAt,
			Radius = radius,
			Seed = ++seedClock,
			Ramp = ramp,
			Tint = tint,
			Count = ImpactDebris.CountFor(radius),
			Live = true,
		};
		next = (next + 1) % MaxBursts;
		SetProcess(true);
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		for (int i = 0; i < MaxBursts; i++)
		{
			if (!bursts[i].Live)
				continue;
			bursts[i].Age += dt;
			if (bursts[i].Age >= ImpactDebris.LifeSeconds)
			{
				bursts[i].Live = false;
				live--;
			}
		}
		QueueRedraw();
		if (live <= 0)
		{
			live = 0;
			SetProcess(false);
		}
	}

	public override void _Draw()
	{
		for (int i = 0; i < MaxBursts; i++)
		{
			Burst b = bursts[i];
			if (b.Live)
				ImpactDebris.Draw(this, ToLocal(b.At), b.Radius, b.Age, b.Seed, b.Ramp, b.Count, b.Tint);
		}
	}
}
