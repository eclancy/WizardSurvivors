using Godot;
using System;

namespace WizardSurvivors.scripts;

/// <summary>
/// Squares on the WORLD's 2 px art grid, for effects stamped with DrawRect from a node that stands
/// anywhere (a seed sown at the wizard's feet, a plague on whoever it caught, a trail behind him).
/// </summary>
/// <remarks>
/// WhipLimb.Cell snaps to the grid of the canvas it is drawn into, which is the world's only when the
/// node itself stands on a whole art pixel. These snap to the world's: the art pixels of a stamped effect
/// then line up with the shader-drawn ground under it (mire_surface, bramble_patch, spore_cloud), which
/// all snap each fragment to the world's 2 px cell (.ai/art-direction.md section 1). Made for the growth
/// spells' pixel-art pass (2026-10-29); nothing allocates.
/// </remarks>
public static class PixelCells
{
	public const float Grid = 2f;

	/// <summary>
	/// A square of <paramref name="size"/> round <paramref name="local"/> (in <paramref name="canvas"/>'s
	/// own frame, which must be unrotated and unscaled), on the world's grid.
	/// </summary>
	public static Rect2 Snap(CanvasItem canvas, Vector2 local, float size)
	{
		Vector2 o = canvas.GetGlobalTransform().Origin;
		float half = size * 0.5f;
		float x = MathF.Round((o.X + local.X - half) / Grid) * Grid - o.X;
		float y = MathF.Round((o.Y + local.Y - half) / Grid) * Grid - o.Y;
		return new Rect2(x, y, size, size);
	}

	/// <summary>A point of <paramref name="canvas"/>'s frame moved onto the nearest corner of the world's grid.</summary>
	public static Vector2 SnapPoint(CanvasItem canvas, Vector2 local)
	{
		Vector2 o = canvas.GetGlobalTransform().Origin;
		return new Vector2(MathF.Round((o.X + local.X) / Grid) * Grid - o.X, MathF.Round((o.Y + local.Y) / Grid) * Grid - o.Y);
	}
}
