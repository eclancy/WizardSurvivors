using Godot;
using System;

public partial class FloatingText : Node2D
{
	[Export] public string Text { get; set; } = "";
	[Export] public Color Color { get; set; } = new Color(1, 1, 1, 1);
	[Export] public float Duration { get; set; } = 0.7f;
	[Export] public float RiseDistance { get; set; } = 24f;
	[Export] public float LabelScale { get; set; } = 2.0f;

	private Label? label;
	private Vector2 startPos;
	private float elapsed = 0f;

	public override void _Ready()
	{
		label = new Label();
		label.Text = Text;
		label.Modulate = Color;
		label.HorizontalAlignment = HorizontalAlignment.Center;
		// Set label scale
		label.Scale = new Vector2(LabelScale, LabelScale);
		AddChild(label);
		startPos = Position;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		elapsed += (float)delta;
		Position = new Vector2(Position.X, startPos.Y - (RiseDistance * (elapsed / Duration)));
		if (label != null)
		{
			var m = label.Modulate;
			m.A = Mathf.Lerp(1f, 0f, elapsed / Duration);
			label.Modulate = m;
		}
		if (elapsed >= Duration) QueueFree();
	}
}
