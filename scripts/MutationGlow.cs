using Godot;

/// <summary>
/// The light round a mutation card: a haze that breathes out of its stained edge, two glints that
/// chase each other round it, and motes that lift off it and fade. Behind the card, always.
/// </summary>
/// <remarks>
/// From the Pixel 9: "mutation is currently colored white. we need to again make the color subtle,
/// around the edges, but the mutation can have an ethereal animated glowing to signify its ability
/// to be mutated." The card went back to the parchment page (tools/art/ui_frames.py, mark
/// "mutation"); this is the other half. The new spell beside it is still, and the upgrade is still
/// but for its one breathing pip - the mutation is the card that is alive all the way round.
///
/// NEVER UNDER A WORD. ShowBehindParent puts every pixel of this behind the page, which is opaque:
/// the haze shows outside the card and through the bites in its torn edge, so the tear itself seems
/// to glow, and the ink on the parchment is exactly as readable as on a page with no glow at all.
///
/// ETHEREAL, NOT AN ELEMENT. The light drifts slowly between two emissive ramps - the wind ramp's
/// pale teal (the mutation's own row is lichen, teal) and the arcane ramp's lilac - over seven
/// seconds, so it never settles into reading as one element's colour. The two rings nearest the
/// page take each ramp's EDGE tone, which is dark enough to show on the light evolution page; the
/// outer rings take the MID tone, which glows on the stone of the level-up screen.
///
/// CALM: the breath is 3.4 s, a glint goes round in 5.5 s, a mote lives 2.8 s. While the card is
/// hovered, focused or pressed it brightens, so the card about to be taken wakes, like BoonAura and
/// LevelPips. The clock is Time.GetTicksMsec, which runs while the level-up has the tree paused, so
/// every card on screen breathes together.
///
/// Cheap: about a dozen rectangle outlines, two dozen short lines and ten small squares a frame, no
/// allocation (Rect2, Vector2 and Color are structs), and a redraw only while visible. ProcessMode
/// Always, or the paused tree would freeze it on one frame. There is no reduced-motion setting yet;
/// if one is added, this should hold still at the top of its breath with no glints or motes.
/// </remarks>
public partial class MutationGlow : Control
{
	/// <summary>How far the haze reaches outside the card. Less than half the narrowest gap between
	/// two cards (8 on a PC level-up row), so it never lies on a neighbour.</summary>
	public const float Reach = 7f;
	/// <summary>How far inside the card's edge it starts: the deepest bite of the torn edge (3
	/// authored, 6 drawn), so the bites are lit and nothing further in is drawn at all.</summary>
	private const float Bite = 6f;
	private const float BreathSeconds = 3.4f;
	private const float HueSeconds = 7.0f;
	private const float SweepSeconds = 5.5f;
	private const float MoteSeconds = 2.8f;
	private const int MoteCount = 10;
	private const int TrailSegments = 12;
	private const float TrailStep = 6f;

	// Palette tones (tools/art/bonelight.py, ELEMENTS): wind and arcane, mid and edge.
	private static readonly Color TealMid = new Color(0.706f, 0.933f, 0.878f);    // wind mid    #B4EEE0
	private static readonly Color TealEdge = new Color(0.310f, 0.682f, 0.620f);   // wind edge   #4FAE9E
	private static readonly Color LilacMid = new Color(0.753f, 0.549f, 1.0f);     // arcane mid  #C08CFF
	private static readonly Color LilacEdge = new Color(0.478f, 0.247f, 0.831f);  // arcane edge #7A3FD4
	private static readonly Color Core = new Color(0.918f, 0.988f, 0.965f);       // wind hot    #EAFCF6

	/// <summary>The card this glow belongs to: hovered, focused or pressed, it brightens.</summary>
	public BaseButton FocusOwner { get; set; }

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		FocusMode = FocusModeEnum.None;
		ProcessMode = ProcessModeEnum.Always;
		ShowBehindParent = true;
		SetAnchorsPreset(LayoutPreset.FullRect);
		OffsetLeft = -Reach;
		OffsetTop = -Reach;
		OffsetRight = Reach;
		OffsetBottom = Reach;
	}

	public override void _Process(double delta)
	{
		if (IsVisibleInTree())
			QueueRedraw();
	}

	private bool IsLit()
	{
		if (FocusOwner == null || !IsInstanceValid(FocusOwner))
			return false;
		return FocusOwner.HasFocus() || FocusOwner.IsHovered() || FocusOwner.ButtonPressed;
	}

	public override void _Draw()
	{
		var card = new Rect2(Reach, Reach, Size.X - Reach * 2f, Size.Y - Reach * 2f);
		if (card.Size.X <= 0f || card.Size.Y <= 0f)
			return;

		float t = Time.GetTicksMsec() / 1000f;
		float breath = 0.5f - 0.5f * Mathf.Cos(t / BreathSeconds * Mathf.Tau);
		float hue = 0.5f - 0.5f * Mathf.Cos(t / HueSeconds * Mathf.Tau);
		bool lit = IsLit();
		float strength = lit ? 1f : 0.85f;
		Color edge = TealEdge.Lerp(LilacEdge, hue);
		Color mid = TealMid.Lerp(LilacMid, hue);

		// THE HAZE: one-pixel rings. Inside the card's edge - only ever seen through the bites of
		// the tear - at full strength, so the tear itself is lit; outside, falling off to nothing at
		// Reach, so it reads as light rather than as a stroke. The three rings at the paper take the
		// edge tone (it shows on the light evolution page), the rest the mid tone (it glows on
		// stone). The breath moves only the haze's alpha, never its size: pixel art that grows and
		// shrinks by fractions of a pixel shimmers.
		float level = strength * (0.45f + 0.45f * breath);
		for (float d = -Bite; d < Reach; d += 1f)
		{
			float k = d < 0f ? 1f : 1f - d / Reach;
			float a = level * Mathf.Pow(k, 1.3f);
			DrawRect(card.Grow(d + 0.5f), new Color(d < 3f ? edge : mid, a), false, 1f);
		}

		// THE GLINTS: two short trails of light going round the page half a turn apart, on the ring
		// just outside its edge - a band of the edge tone with a core of light down its middle, so
		// it shows on the light page and the dark stone alike.
		var track = card.Grow(2f);
		float perimeter = 2f * (track.Size.X + track.Size.Y);
		float head = t / SweepSeconds % 1f * perimeter;
		for (int pass = 0; pass < 2; pass++)
		{
			float start = head + pass * perimeter * 0.5f;
			for (int j = 0; j < TrailSegments; j++)
			{
				float s = start - j * TrailStep;
				Vector2 from = Perimeter(track, s - TrailStep, out _);
				Vector2 to = Perimeter(track, s, out _);
				float fade = strength * (1f - (float)j / TrailSegments);
				DrawLine(from, to, new Color(edge, 0.8f * fade), 4f);
				DrawLine(from, to, new Color(j < 3 ? Core : mid, fade), 2f);
			}
		}

		// THE MOTES: each rises off a point of the edge, drifts out and up, and fades; every life
		// starts somewhere new along the edge (golden-ratio steps, so they never bunch). The same
		// two tones: a 2x2 of the edge tone with a pixel of light in it.
		for (int i = 0; i < MoteCount; i++)
		{
			float life = t / MoteSeconds + i * 0.618034f;
			float phase = life % 1f;
			float cycle = Mathf.Floor(life);
			float along = (i * 0.381966f + cycle * 0.754878f) % 1f;
			Vector2 origin = Perimeter(track, along * perimeter, out Vector2 normal);
			Vector2 at = (origin + normal * (2f + 5f * phase) + Vector2.Up * (6f * phase)).Round();
			float a = strength * Mathf.Sin(phase * Mathf.Pi);
			DrawRect(new Rect2(at - Vector2.One, new Vector2(2f, 2f)), new Color(edge, a), true);
			DrawRect(new Rect2(at - Vector2.One, Vector2.One), new Color(Core, a), true);
		}
	}

	/// <summary>The point <paramref name="s"/> pixels clockwise round a rectangle from its top left,
	/// and the outward normal of the side it lies on.</summary>
	private static Vector2 Perimeter(Rect2 r, float s, out Vector2 normal)
	{
		float w = r.Size.X;
		float h = r.Size.Y;
		float p = 2f * (w + h);
		s = (s % p + p) % p;
		if (s < w)
		{
			normal = Vector2.Up;
			return r.Position + new Vector2(s, 0f);
		}
		s -= w;
		if (s < h)
		{
			normal = Vector2.Right;
			return r.Position + new Vector2(w, s);
		}
		s -= h;
		if (s < w)
		{
			normal = Vector2.Down;
			return r.Position + new Vector2(w - s, h);
		}
		s -= w;
		normal = Vector2.Left;
		return r.Position + new Vector2(0f, h - s);
	}
}
