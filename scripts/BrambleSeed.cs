using Godot;
using System;
using WizardSurvivors.scripts;

// Bramble Seed: a seed dropped at the player's feet that grows into a thorn patch over several
// seconds (.ai/side-events.md). Grown back in the corrupted grove of the Enchanted Forest.
//
// **The payload nothing else has: it gets bigger the longer it lives.** Every zone in the game is
// at full size the moment it appears. A seed is small and harmless for its first seconds and wide
// and dangerous at the end, so its value is in where the player will be later rather than where
// they are now - plant, kite a loop, and drag the swarm back across what has grown.
//
// **Pure Grass.** Grass had no pure spell, so it could never reach its capstone.
//
// At most MaxAlive patches stand at once; a new seed past that withers the oldest.
public partial class BrambleSeed : Node2D
{
	[Export] public float GrowSeconds { get; set; } = 6f;
	[Export] public float HoldSeconds { get; set; } = 3f;
	[Export] public float StartRadius { get; set; } = 16f;
	[Export] public float MaxRadius { get; set; } = 110f;
	[Export] public float TickSeconds { get; set; } = 0.5f;
	[Export] public float SlowMultiplier { get; set; } = 0.6f;

	public const int MaxAlive = 3;

	// Wildwood, Vainamoinen's Apotheosis (verb: swarm). A patch sown by the player is generation 0;
	// once it finishes growing it throws WildwoodSeeds of its own at its edge, generation 1, which
	// do not seed again. Bounded by construction: at most MaxAlive x (1 + WildwoodSeeds) patches.
	public int Generation { get; set; } = 0;
	private const int WildwoodSeeds = 3;
	private bool seeded;

	public int Damage { get; set; } = 7;
	public SpellData SpellData { get; set; }
	public Player PlayerRef { get; set; }

	private float age;
	private float tickTimer;
	private const int ThornCount = 14;

	private static readonly Color Ground = new Color(0.18f, 0.24f, 0.12f, 0.45f);
	private static readonly Color Thorn = new Color(0.36f, 0.56f, 0.24f, 0.95f);
	private static readonly Color ThornLit = new Color(0.62f, 0.84f, 0.42f, 0.95f);

	public float Radius => Mathf.Lerp(StartRadius, MaxRadius, Mathf.Clamp(age / GrowSeconds, 0f, 1f));

	public override void _Ready() => ZIndex = 1;

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		age += dt;
		QueueRedraw();
		if (age >= GrowSeconds + HoldSeconds)
		{
			QueueFree();
			return;
		}

		if (!seeded && Generation == 0 && age >= GrowSeconds && SpellData?.IsApotheosis == true)
		{
			seeded = true;
			Scatter();
		}

		tickTimer -= dt;
		if (tickTimer > 0f)
			return;
		tickTimer = TickSeconds;

		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;

		float radiusSquared = Radius * Radius;
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Enemy enemy || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			if (GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition) > radiusSquared)
				continue;
			PlayerRef.DealDamageToEnemy(enemy, Damage, source: SpellData);
			enemy.ApplySlow(SlowMultiplier, TickSeconds + 0.2f);
		}
	}

	private void Scatter()
	{
		var scene = GD.Load<PackedScene>(SceneFilePath);
		Node parent = GetParent();
		if (scene == null || parent == null)
			return;
		float turn = (float)GD.RandRange(0.0, Mathf.Tau);
		for (int i = 0; i < WildwoodSeeds; i++)
		{
			if (scene.Instantiate() is not BrambleSeed child)
				continue;
			child.Generation = Generation + 1;
			child.SpellData = SpellData;
			child.PlayerRef = PlayerRef;
			child.Damage = Damage;
			child.MaxRadius = MaxRadius * 0.8f;
			child.HoldSeconds = HoldSeconds;
			parent.AddChild(child);
			float a = turn + i * Mathf.Tau / WildwoodSeeds;
			child.GlobalPosition = GlobalPosition + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * MaxRadius * 1.6f;
		}
	}

	public override void _Draw()
	{
		float r = Radius;
		float fade = Mathf.Clamp((GrowSeconds + HoldSeconds - age) / 0.4f, 0f, 1f);
		DrawCircle(Vector2.Zero, r, new Color(Ground, Ground.A * fade));
		// Thorns radiate out to the current edge, so the patch visibly reaches further each second.
		for (int i = 0; i < ThornCount; i++)
		{
			float a = i * Mathf.Tau / ThornCount + (i % 2) * 0.2f;
			Vector2 dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
			Vector2 tip = dir * r;
			Vector2 side = new Vector2(-dir.Y, dir.X) * 4f;
			// Only the outer band. Full-length rays read fine on a small patch and turned a level 8
			// Wildwood thicket into a screen of green spokes; the edge is the part that says where it reaches.
			DrawLine(dir * MathF.Max(r * 0.25f, r - 34f), tip, new Color(Thorn, fade), 2.5f);
			DrawLine(tip, tip - dir * 7f + side, new Color(ThornLit, fade), 2f);
			DrawLine(tip, tip - dir * 7f - side, new Color(ThornLit, fade), 2f);
		}
		DrawCircle(Vector2.Zero, 4f, new Color(ThornLit, fade));
	}
}
