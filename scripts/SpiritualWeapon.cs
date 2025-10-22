using Godot;
using System;

namespace WizardSurvivors.scripts;

public partial class SpiritualWeapon : Node2D
{

	public Weapon Weapon { get; set; }
	public Node2D PlayerRef;

	private Node2D[] orbitingObjects;
	private float orbitAngle = 0f;

	public override void _Ready()
	{
		GD.Print("SpiritualWeapon ready");
		if (Weapon == null) return;
		orbitingObjects = new Node2D[Weapon.NumberOfProjectiles];
		for (int i = 0; i < Weapon.NumberOfProjectiles; i++)
		{
			var area = new Area2D();
			var sprite = new Sprite2D();
			sprite.Texture = GD.Load<Texture2D>("res://assets/Slime.png"); // Placeholder texture
			area.AddChild(sprite);
			var shape = new CollisionShape2D();
			var circle = new CircleShape2D();
			circle.Radius = 12f; // Adjust as needed or use Weapon.Area
			shape.Shape = circle;
			area.AddChild(shape);
			area.Monitoring = true;
			area.Monitorable = true;
			area.Connect("area_entered", new Callable(this, nameof(OnAreaEntered)));
			area.Connect("body_entered", new Callable(this, nameof(OnBodyEntered)));
			AddChild(area);
			orbitingObjects[i] = area;
		}
	}

	// Collision handlers
	private void OnAreaEntered(Area2D area)
	{
		if (area.IsInGroup("enemies"))
		{
			GD.Print("SpiritualWeapon hit enemy (area)");
			area.Call("TakeDamage", Weapon.Damage);
		}
	}

	private void OnBodyEntered(Node body)
	{
		if (body.IsInGroup("enemies"))
		{
			GD.Print("SpiritualWeapon hit enemy (body)");
			body.Call("TakeDamage", Weapon.Damage);
		}
	}

	public override void _Process(double delta)
	{
		// Always center on player (since this is a child of Player)
		Position = Vector2.Zero;
		if (Weapon == null || orbitingObjects == null) return;

		// Orbit logic
		orbitAngle += Weapon.AttackSpeed * (float)delta;
		float angleStep = 2f * Mathf.Pi / Weapon.NumberOfProjectiles;
		for (int i = 0; i < Weapon.NumberOfProjectiles; i++)
		{
			float angle = orbitAngle + i * angleStep;
			Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Weapon.Range;
			orbitingObjects[i].Position = offset;
		}
	}
}
