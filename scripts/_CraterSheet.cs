using Godot;
using WizardSurvivors.scripts;

// A contact sheet of craters for judging scenes/shaders/crater.gdshader and CraterFire by eye, without
// a crowd standing in them. Photograph it through _UiShot:
//   "$GODOT" --path . --resolution 1280x720 scenes/_UiShot.tscn -- --scene=res://scenes/_CraterSheet.tscn --shot=craters --no-save-writes
// Top row: a plain impact through its life (flash, molten, crusting, cold, dissolving). Middle row: a
// Magma Crater burning, then going out. Bottom row: the radius range Area reaches. Each column stands on
// a different floor (stone, the forest's teal, the sands) because a crater must read on all of them.
// --draws prints the frame's draw calls every frame (--no-ground / --no-fire hide one layer), which is
// how the batching was measured: the floor is one call, every crater's hole one more, their fire two.
public partial class _CraterSheet : Node2D
{
	private static readonly Color[] Floors = { new("#1E2634"), new("#24404A"), new("#54362E") };
	private static readonly float[] PlainAges = { 0.05f, 0.22f, 0.55f, 0.95f, 1.6f, 2.15f };
	private static readonly float[] BurnAges = { 0.3f, 1.4f, 2.6f, 3.6f, 4.3f, 5.0f };
	private static readonly float[] Radii = { 30f, 60f, 90f, 130f };
	private const float Burn = 4f;

	private Node2D ground;
	private Node2D fire;
	private float clock;

	public override void _Ready()
	{
		for (int i = 0; i < 6; i++)
		{
			AddChild(new ColorRect
			{
				Color = Floors[i % Floors.Length],
				Position = new Vector2(i * 213f, 0f),
				Size = new Vector2(214f, 720f),
				ZIndex = -100,
			});
		}
		ground = new Node2D { Name = "Ground", ZIndex = CraterGround.GroundZ, Material = CraterGround.Material };
		fire = new Node2D { Name = "Fire", ZIndex = CraterGround.FireZ };
		AddChild(ground);
		AddChild(fire);
		ground.Draw += DrawGround;
		fire.Draw += DrawFire;
	}

	public override void _Process(double delta)
	{
		clock += (float)delta;
		ground.QueueRedraw();
		fire.QueueRedraw();
		if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--draws") >= 0)
		{
			ground.Visible = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-ground") < 0;
			fire.Visible = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-fire") < 0;
			GD.Print($"DRAWS {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)}");
		}
	}

	private static Vector2 Plain(int i) => new(106f + i * 213f, 110f);
	private static Vector2 Burning(int i) => new(106f + i * 213f, 330f);
	private static Vector2 Sized(int i) => new(106f + i * 320f, 580f);

	private void DrawGround()
	{
		for (int i = 0; i < PlainAges.Length; i++)
			DrawOne(Plain(i), 60f, PlainAges[i], 0f, (uint)(i * 7 + 1));
		for (int i = 0; i < BurnAges.Length; i++)
			DrawOne(Burning(i), 60f, BurnAges[i], Burn, (uint)(i * 7 + 2));
		for (int i = 0; i < Radii.Length; i++)
			DrawOne(Sized(i), Radii[i], 0.6f, 0f, (uint)(i * 7 + 3));
	}

	private void DrawOne(Vector2 at, float radius, float age, float burn, uint seed)
	{
		float life = burn + 1.3f + 1.0f;
		float heat = CraterGround.HeatAt(age, burn, 1.3f, seed);
		CraterGround.Draw(ground, at, radius, heat, age, Mathf.Clamp((life - age) / 0.8f, 0f, 1f));
	}

	private void DrawFire()
	{
		// The fire's own frames move with the clock, so a burst of photographs shows it flicker.
		float wobble = clock % 0.5f;
		for (int i = 0; i < PlainAges.Length; i++)
			CraterFire.DrawFire(fire, Plain(i), 60f, PlainAges[i], 0f, (uint)(i * 7 + 1), Colors.White);
		for (int i = 0; i < BurnAges.Length; i++)
			CraterFire.DrawFire(fire, Burning(i), 60f, BurnAges[i] + wobble * 0.2f, Burn, (uint)(i * 7 + 2), Colors.White);
		for (int i = 0; i < Radii.Length; i++)
			CraterFire.DrawFire(fire, Sized(i), Radii[i], 0.6f, 0f, (uint)(i * 7 + 3), Colors.White);
		for (int i = 0; i < PlainAges.Length; i++)
			CraterFire.DrawEmbers(fire, Plain(i), 60f, PlainAges[i], 0f, (uint)(i * 7 + 1), Colors.White);
		for (int i = 0; i < BurnAges.Length; i++)
			CraterFire.DrawEmbers(fire, Burning(i), 60f, BurnAges[i] + wobble * 0.2f, Burn, (uint)(i * 7 + 2), Colors.White);
	}
}
