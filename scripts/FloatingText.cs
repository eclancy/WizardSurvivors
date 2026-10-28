using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

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

	// A damage number per hit is the busiest thing a swarm makes: at a full arena the spells land
	// several hits a physics step, and instancing the scene, building a Label and adding it cost
	// about 0.4 ms each on the desktop (.ai/performance.md). So a number that has faded goes back to
	// a pool, hidden and idle in the tree where it stood, and the next hit on the same parent takes
	// it up again - the Label is kept, only its text and tint change. Looks exactly as before.
	private const int MaxPooled = 256;
	private static readonly Stack<FloatingText> pool = new();

	/// <summary>
	/// Shows <paramref name="text"/> rising from <paramref name="globalPosition"/> under
	/// <paramref name="parent"/>, reusing a faded number from the pool when there is one there.
	/// </summary>
	public static void Show(Node parent, PackedScene scene, string text, Color color, Vector2 globalPosition, float scale = 1f)
	{
		FloatingText? ft = null;
		while (pool.Count > 0)
		{
			FloatingText candidate = pool.Pop();
			// A pooled number belongs to the arena it faded in; one from a scene since freed, or
			// under another parent, is not reused.
			if (IsInstanceValid(candidate) && candidate.IsInsideTree() && candidate.GetParent() == parent)
			{
				ft = candidate;
				break;
			}
		}

		if (ft != null)
		{
			ft.Text = text;
			ft.Color = color;
			ft.Scale = new Vector2(scale, scale);
			ft.GlobalPosition = globalPosition;
			ft.Restart();
			return;
		}

		var node = scene.Instantiate<Node2D>();
		if (node is FloatingText fresh)
		{
			fresh.Text = text;
			fresh.Color = color;
			fresh.GlobalPosition = globalPosition;
			if (scale != 1f)
				fresh.Scale *= scale;
		}
		else
		{
			node.Set("Text", text);
			node.Set("Color", color);
			node.Set("GlobalPosition", globalPosition);
		}
		parent.AddChild(node);
	}

	public override void _Ready()
	{
		// Over everything in the world: a number sorted in with the bodies was hidden by every body
		// south of it (DrawLayers).
		ZAsRelative = false;
		ZIndex = DrawLayers.FloatingText;
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

	// Back to the state _Ready leaves a new one in, at the position it was just given.
	private void Restart()
	{
		elapsed = 0f;
		startPos = Position;
		if (label != null)
		{
			label.Text = Text;
			label.Modulate = Color;
		}
		Visible = true;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		var perfStart = FrameProfiler.BeginSampledFrame();
		elapsed += (float)delta;
		Position = new Vector2(Position.X, startPos.Y - (RiseDistance * (elapsed / Duration)));
		if (label != null)
		{
			var m = label.Modulate;
			m.A = Mathf.Lerp(1f, 0f, elapsed / Duration);
			label.Modulate = m;
		}
		if (elapsed >= Duration)
			Retire();
		FrameProfiler.End(FrameProfiler.Section.FloatingText, perfStart);
	}

	private void Retire()
	{
		if (pool.Count >= MaxPooled)
		{
			QueueFree();
			return;
		}
		Visible = false;
		SetProcess(false);
		pool.Push(this);
	}
}
