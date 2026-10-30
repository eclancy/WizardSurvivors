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
///
/// Pixel art (2026-10-29): the heads were rotated polygons over a 90% outline. They are one quad drawn by
/// scenes/shaders/hunters_mark.gdshader, snapped to the world's 2 px grid, the arrow's Metal point and
/// Wind flanks on an art pixel of occlusion; the mark dissolves through the Bayer matrix as it runs out.
/// </remarks>
public partial class HuntersMark : Node2D
{
	private const float Ring = 30f;

	// The heads' length, point to base: the point stands this far inside Ring.
	private const float HeadLength = 10f;

	private Enemy host;
	private float left;
	private float spin;

	private static Shader markShader;
	private static readonly StringName ParamSpin = "spin";
	private static readonly StringName ParamRing = "ring";
	private static readonly StringName ParamLeft = "left";
	private ShaderMaterial material;

	public override void _Ready()
	{
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
		TopLevel = true;
		Visible = false;
		markShader ??= GD.Load<Shader>("res://scenes/shaders/hunters_mark.gdshader");
		material = new ShaderMaterial { Shader = markShader };
		Material = material;
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
		// On the art grid, so the heads' pixels line up with everything else's.
		Vector2 at = host.GlobalPosition + new Vector2(0f, -10f);
		GlobalPosition = new Vector2(Mathf.Round(at.X / WhipLimb.Grid) * WhipLimb.Grid, Mathf.Round(at.Y / WhipLimb.Grid) * WhipLimb.Grid);
		float pulse = 0.88f + 0.12f * MathF.Sin(spin * 7f);
		material.SetShaderParameter(ParamSpin, spin);
		material.SetShaderParameter(ParamRing, Ring * pulse - HeadLength * 0.6f);
		material.SetShaderParameter(ParamLeft, left < 0.5f ? left / 0.5f : 1f);
	}

	// One quad over the four heads at their widest; the shader decides every pixel.
	public override void _Draw()
	{
		float r = Ring + HeadLength + 8f;
		DrawRect(new Rect2(-r, -r, r * 2f, r * 2f), Colors.White);
	}
}
