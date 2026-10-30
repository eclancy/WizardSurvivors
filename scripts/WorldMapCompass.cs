using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

/// <summary>
/// The heading strip across the top of the world map (.ai/world-map.md, section 7): a mark for every
/// stop, sliding with the wizard so the caret at its centre is where he is, the selected stop lit.
/// </summary>
/// <remarks>
/// Eric: "a compass style scroll bar at the top of the screen so you can see what levels you can
/// scroll to." It slides by the wizard's position between stops (WorldMapView.WizardStopProgress),
/// not by the press, so it glides while he walks and stops when he does.
///
/// It takes no focus. A pad steps with left and right, which this shows; a focus stop for the stick
/// here would be a second selection fighting the first. A tap on a mark jumps to it - on a phone it
/// is the quickest way from the first chapter to the Sketchbook, and the walk is still a walk.
///
/// The marks are 16x16 art saved x2 (tools/art/world_map.py, mark-*.png), drawn at a whole number of
/// screen pixels per texture pixel. The numerals are the engine's sans at the Label role, in an ink
/// measured against the band (.ai/level-up-screen.md 4a).
/// </remarks>
public partial class WorldMapCompass : Control
{
	public enum MarkKind
	{
		Open,
		Cleared,
		Locked,
		Rush,
		Page,
	}

	public sealed class Mark
	{
		public MarkKind Kind { get; set; }
		/// <summary>The numeral under it (the campaign position), or empty.</summary>
		public string Caption { get; set; } = string.Empty;
		/// <summary>Draws a break before this mark: the stops outside the campaign sit apart.</summary>
		public bool BreakBefore { get; set; }
	}

	// The band: stone deep with a gold rule at its foot, the level-up heading band's materials.
	public static readonly Color Band = new("#0B0F18");
	public static readonly Color BandLip = new("#1E2634");
	public static readonly Color Rule = new("#8A6A1E");
	public static readonly Color Caret = new("#D8B04A");
	// Warm bone on stone deep: 16.9:1. The dimmed numerals (a locked stop's) are skin.base, 6.9:1.
	public static readonly Color Ink = new(1.0f, 0.95f, 0.86f);
	public static readonly Color InkLocked = new("#9AA4B8");

	private readonly List<Mark> marks = new();
	private readonly Dictionary<MarkKind, Texture2D> textures = new();
	private int selected;
	private float progress;
	private float spacing = 84f;

	/// <summary>A mark was tapped.</summary>
	public event Action<int> MarkPressed;

	public WorldMapCompass()
	{
		MouseFilter = MouseFilterEnum.Stop;
		TextureFilter = TextureFilterEnum.Nearest;
		FocusMode = FocusModeEnum.None;
		// A mark sliding off the end must not draw its numeral under the Back button beside it.
		ClipContents = true;
	}

	public override void _Ready()
	{
		foreach (MarkKind kind in Enum.GetValues(typeof(MarkKind)))
		{
			string path = WorldMapLayout.Directory + "mark-" + kind.ToString().ToLowerInvariant() + ".png";
			if (ResourceLoader.Exists(path))
				textures[kind] = GD.Load<Texture2D>(path);
			else
				GD.PushError($"WorldMapCompass: '{path}' is missing or not imported.");
		}
		spacing = ResponsiveLayout.IsNarrow(this) ? 78f : 88f;
	}

	public void SetMarks(IEnumerable<Mark> source)
	{
		marks.Clear();
		marks.AddRange(source);
		QueueRedraw();
	}

	public void SetMark(int index, MarkKind kind)
	{
		if (index >= 0 && index < marks.Count && marks[index].Kind != kind)
		{
			marks[index].Kind = kind;
			QueueRedraw();
		}
	}

	/// <summary>The selected stop and where the wizard is (a fractional stop index).</summary>
	public void SetState(int selectedIndex, float wizardProgress)
	{
		if (selectedIndex == selected && Mathf.IsEqualApprox(wizardProgress, progress))
			return;
		selected = selectedIndex;
		progress = wizardProgress;
		QueueRedraw();
	}

	private float XOf(int index) => Size.X / 2f + (index - progress) * spacing + BreaksBefore(index) * spacing * 0.5f
		- BreaksBefore(progress) * spacing * 0.5f;

	// A break counts half a step, so the stops outside the campaign sit visibly apart.
	private float BreaksBefore(float index)
	{
		float total = 0f;
		for (int i = 0; i < marks.Count; i++)
		{
			if (!marks[i].BreakBefore)
				continue;
			// Fractionally, so the strip glides across a break rather than jumping it.
			total += Mathf.Clamp(index - (i - 1), 0f, 1f);
		}
		return total;
	}

	public override void _Draw()
	{
		Vector2 size = Size;
		DrawRect(new Rect2(Vector2.Zero, size), Band);
		DrawRect(new Rect2(0f, 0f, size.X, 2f), BandLip);
		DrawRect(new Rect2(0f, size.Y - 3f, size.X, 3f), Rule);

		Transform2D toScreen = GetViewport().GetFinalTransform() * GetGlobalTransformWithCanvas();
		float screenPerUnit = Mathf.Max(0.0001f, toScreen.X.Length());
		Transform2D fromScreen = toScreen.AffineInverse();
		// Texture pixels are half an art pixel (saved x2): one screen pixel per texture pixel per
		// canvas unit, whole, so a mark is 32 canvas units on a 1:1 window.
		int pixels = Math.Max(1, (int)Math.Round(screenPerUnit, MidpointRounding.AwayFromZero));
		float unitsPerTexel = pixels / screenPerUnit;

		Font font = ThemeDB.FallbackFont;
		int fontSize = ResponsiveLayout.FontSize(this, ResponsiveLayout.TextRole.Label);
		float markTop = 6f;

		for (int i = 0; i < marks.Count; i++)
		{
			float x = XOf(i);
			if (x < -spacing || x > size.X + spacing)
				continue;
			// Marks thin out toward the ends of the band.
			float edge = Mathf.Min(x, size.X - x) / Mathf.Max(1f, size.X * 0.18f);
			float alpha = Mathf.Clamp(edge, 0.15f, 1f);

			if (marks[i].BreakBefore)
			{
				float bx = (XOf(i - 1) + x) / 2f;
				for (float y = 10f; y < size.Y - 10f; y += 6f)
					DrawRect(new Rect2(bx - 1f, y, 2f, 3f), new Color(Rule, alpha));
			}

			bool isSelected = i == selected;
			if (textures.TryGetValue(marks[i].Kind, out Texture2D texture) && texture != null)
			{
				Vector2 drawn = texture.GetSize() * unitsPerTexel;
				Vector2 topLeft = new Vector2(x - drawn.X / 2f, markTop);
				topLeft = fromScreen * (toScreen * topLeft).Round();
				if (isSelected)
				{
					// A lit plate behind the selected mark: the one thing on the strip that is lit.
					var plate = new Rect2(topLeft - new Vector2(5f, 4f), drawn + new Vector2(10f, 8f));
					DrawRect(plate, new Color(Caret, 0.28f));
					DrawRect(plate, Caret, false, 2f);
				}
				DrawTextureRect(texture, new Rect2(topLeft, drawn), false, new Color(1f, 1f, 1f, alpha));
			}

			string caption = marks[i].Caption;
			if (!string.IsNullOrEmpty(caption) && font != null)
			{
				Vector2 textSize = font.GetStringSize(caption, HorizontalAlignment.Left, -1, fontSize);
				Color ink = marks[i].Kind == MarkKind.Locked ? InkLocked : Ink;
				DrawString(font, new Vector2(x - textSize.X / 2f, size.Y - 8f), caption,
					HorizontalAlignment.Left, -1, fontSize, new Color(ink, alpha));
			}
		}

		// The caret: where he is, fixed at the centre.
		float cx = Mathf.Round(size.X / 2f);
		DrawColoredPolygon(new[] { new Vector2(cx - 7f, 0f), new Vector2(cx + 7f, 0f), new Vector2(cx, 7f) }, Caret);
	}

	/// <summary>The middle of a mark, on this control. For the harness's taps.</summary>
	public Vector2 LocalOfMark(int index) => new(XOf(index), Size.Y / 2f);

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press)
			return;
		int best = -1;
		float bestDistance = spacing * 0.5f;
		for (int i = 0; i < marks.Count; i++)
		{
			float d = Mathf.Abs(XOf(i) - press.Position.X);
			if (d < bestDistance)
			{
				bestDistance = d;
				best = i;
			}
		}
		if (best >= 0)
		{
			MarkPressed?.Invoke(best);
			AcceptEvent();
		}
	}
}
