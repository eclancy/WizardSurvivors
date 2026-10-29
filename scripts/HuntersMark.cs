using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// Hunter's Draw's Hunter's Mark (.ai/spell-roster.md 2e): the sigil a full draw leaves on the strongest
/// enemy in reach, which the whole volley seeks and which takes more from everything while it wears it
/// (Enemy.ApplyVulnerable, held for the same seconds this is drawn).
/// </summary>
/// <remarks>
/// The bow already brackets the enemy the next arrow is aimed at, but only while it is drawn
/// (BowDrawVisual). This stays after the volley: four arrowheads pointing in at the quarry, turning
/// slowly round it, so the player can see which one the rest of the loadout should be working on. One
/// mark at a time - a new full draw moves it. On a mark, not an area: it is drawn round one body.
/// In the air (DrawLayers.Air), over the body it marks.
/// </remarks>
public partial class HuntersMark : Node2D
{
	private const float Ring = 30f;

	private Enemy host;
	private float left;
	private float spin;
	private readonly Vector2[] head = new Vector2[3];

	private static readonly Color Outline = new Color(0.02f, 0.03f, 0.05f, 0.9f);
	private static readonly Color Ink = new Color(0.60f, 1.0f, 0.85f, 1f);

	public override void _Ready()
	{
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		TopLevel = true;
		Visible = false;
	}

	/// <summary>Marks <paramref name="quarry"/> for <paramref name="seconds"/>, taking the mark off whatever wore it.</summary>
	public void Mark(Enemy quarry, float seconds)
	{
		host = quarry;
		left = seconds;
		Visible = quarry != null;
		if (quarry != null)
			GlobalPosition = quarry.GlobalPosition;
	}

	public override void _Process(double delta)
	{
		if (host == null)
			return;
		left -= (float)delta;
		if (left <= 0f || !IsInstanceValid(host) || host.IsDying)
		{
			host = null;
			Visible = false;
			return;
		}
		spin += (float)delta * 0.9f;
		GlobalPosition = host.GlobalPosition + new Vector2(0f, -10f);
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (host == null)
			return;
		float alpha = left < 0.5f ? left / 0.5f : 1f;
		float pulse = 0.88f + 0.12f * MathF.Sin(spin * 7f);
		float ring = Ring * pulse;
		for (int pass = 0; pass < 2; pass++)
		{
			Color c = pass == 0 ? Outline with { A = Outline.A * alpha } : Ink with { A = alpha };
			float grow = pass == 0 ? 2.5f : 0f;
			for (int k = 0; k < 4; k++)
			{
				float a = spin + Mathf.Tau * k / 4f;
				Vector2 outward = new Vector2(MathF.Cos(a), MathF.Sin(a));
				Vector2 side = outward.Orthogonal();
				// An arrowhead on the ring, pointing in at the quarry.
				head[0] = outward * (ring - 6f - grow);
				head[1] = outward * (ring + 4f + grow) + side * (6f + grow);
				head[2] = outward * (ring + 4f + grow) - side * (6f + grow);
				DrawColoredPolygon(head, c);
			}
		}
	}
}
