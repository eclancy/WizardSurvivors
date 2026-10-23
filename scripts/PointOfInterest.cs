using System.Collections.Generic;
using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// A place on this run's map worth walking to: a side event, one of its parts (a bog lantern), or
/// the wayfinder that reveals them. See .ai/side-events.md, "Finding the event".
/// </summary>
/// <remarks>
/// HIDDEN UNTIL SEEN OR REVEALED. A point is placed from the run seed at stage start and is there
/// all run, but nothing marks it until the player has either SEEN it (it came onto his screen) or
/// taken the chapter's wayfinder, which reveals every point at once. Once marked, it keeps an arrow
/// at the edge of the screen (<c>PoiPointer</c>) for as long as it is off screen and not done - so a
/// lantern glimpsed at the edge of the view is not lost by walking away from it. A wayfinder is
/// never revealed by itself: it is the thing that reveals.
/// </remarks>
public sealed class PointOfInterest
{
	public PointOfInterest(Vector2 position, string glyph, bool isWayfinder = false)
	{
		Position = position;
		Glyph = glyph;
		IsWayfinder = isWayfinder;
	}

	/// <summary>World position. Fixed for the run.</summary>
	public Vector2 Position { get; }

	/// <summary>Which pictogram its arrow carries (ExploreArt.GlyphPath).</summary>
	public string Glyph { get; }

	/// <summary>The compass (or the chapter's own wayfinder), not something it reveals.</summary>
	public bool IsWayfinder { get; }

	/// <summary>Finished with: a lantern snuffed, an event won or stood down, a wayfinder taken.</summary>
	public bool Done { get; set; }

	/// <summary>Came onto the player's screen at least once.</summary>
	public bool Seen { get; set; }

	/// <summary>True while it earns an arrow: not done, and either seen or revealed.</summary>
	public bool Marked => !Done && (Seen || (PointsOfInterest.Revealed && !IsWayfinder));
}

/// <summary>
/// This run's points of interest. Static because the arrows read it every frame and the run has
/// exactly one; <see cref="Reset"/> runs when a run's SideEventDirector is created and again when
/// it leaves, so nothing outlives its run.
/// </summary>
public static class PointsOfInterest
{
	/// <summary>Every point placed this run. A handful at most; read without allocating.</summary>
	public static readonly List<PointOfInterest> Live = new();

	/// <summary>True once the chapter's wayfinder has been taken: every point is marked from then on.</summary>
	public static bool Revealed { get; private set; }

	/// <summary>
	/// Where the side event's own objective arrow is pointing, if anywhere. The POI arrow for the same
	/// spot is skipped, so a cell the player is carrying a key to is marked once, not twice.
	/// </summary>
	public static Vector2? ObjectiveTarget { get; set; }

	/// <summary>True during the boss fight: a duel, so no arrows to anywhere else.</summary>
	public static bool Suspended { get; set; }

	public static PointOfInterest Add(PointOfInterest point)
	{
		Live.Add(point);
		return point;
	}

	public static void Reveal() => Revealed = true;

	public static void Reset()
	{
		Live.Clear();
		Revealed = false;
		ObjectiveTarget = null;
		Suspended = false;
	}

	/// <summary>True if any point currently earns an arrow. Cheap: lets the arrows skip redrawing.</summary>
	public static bool AnyMarked
	{
		get
		{
			foreach (PointOfInterest p in Live)
				if (p.Marked)
					return true;
			return false;
		}
	}

	/// <summary>
	/// Marks as seen every point inside <paramref name="visibleWorld"/> - the camera's view of the
	/// world, grown a little so a point whose glow is already at the edge counts.
	/// </summary>
	public static void UpdateSeen(Rect2 visibleWorld)
	{
		foreach (PointOfInterest p in Live)
		{
			if (!p.Seen && visibleWorld.HasPoint(p.Position))
				p.Seen = true;
		}
	}
}
