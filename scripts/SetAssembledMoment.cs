using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

/// <summary>
/// The moment a Full Set Enchantment assembles (issue #43): its pieces fly in from the sides and
/// meet, there is a flash, and the assembled object stands in the upper middle of the screen with
/// the set's name and what it now does, then fades.
/// </summary>
/// <remarks>
/// The set icons were redrawn as pieces of one object (tools/art/relic_icons.py, #63) - the harness of
/// one armour, the jewels of one crown - so the payoff can be literal: the pieces you collected are
/// drawn coming together into the thing they were parts of. Until this, completing a set mid-run
/// showed nothing at all; the relic strip that used to mark it is no longer built in a run
/// (Node2DGame), so the set's only trace was a line in the pause screen.
///
/// It takes no input and blocks nothing: every control ignores the mouse, so a tap goes through to
/// the touch stick under it, and the run carries on while it plays. It sits on layer 7 - above the
/// stick (5) and the HUD, below the level-up menu (10) and the chest menu (100) - and it is Pausable,
/// so a set completed by picking a relic waits, hidden, until the chest menu closes and the run
/// resumes, and a level-up that opens mid-moment freezes it under the menu instead of letting it
/// play out unseen. Two sets completed by one relic play one after the other.
/// </remarks>
public partial class SetAssembledMoment : CanvasLayer
{
	private const int MomentLayer = 7;

	// The timeline, in seconds from the start of a moment.
	private const float GatherSeconds = 0.55f;   // pieces fly in and meet
	private const float PopSeconds = 0.22f;      // the object overshoots and settles
	private const float TextInSeconds = 0.3f;    // the plate fades in after the flash
	private const float HoldUntil = 3.6f;        // when it starts to go
	private const float FadeSeconds = 0.6f;
	private const float RingSeconds = 0.6f;      // the burst ring travels out from the flash

	// 64 px icons at whole multiples, so the pixel art stays crisp: the assembled object at x3, the
	// pieces at x2 while they travel.
	private const float IconSource = 64f;
	private const float ObjectScale = 3f;
	private const float PieceScale = 2f;

	// Where it all stands: in the band between the HUD and the player's head - the camera keeps him
	// at the centre of the screen, and the first layout, a third of the way down, put the plate
	// straight over him. Side by side on a wide screen, where that band is short (about 190 px of a
	// 720 canvas); stacked on a phone, where it is tall and narrow.
	private const float WideHudBand = 110f;
	private const float NarrowHudBand = 150f;
	private const float PlayerHeadroom = 56f;
	private const float PlateGap = 18f;
	private const float WidePlateWidth = 480f;
	private const float NarrowPlateMaxWidth = 560f;
	private const float SideMargin = 32f;

	private static readonly Color Gold = CodexRows.Stone.Heading;
	private static readonly Color GoldDeep = CodexRows.Stone.Reward;

	private readonly Queue<ChestSetDefinition> pending = new();
	private ChestSetDefinition? showing;
	private float clock;
	private bool flashed;

	private Control root = null!;
	private BurstDrawing burst = null!;
	private TextureRect objectIcon = null!;
	private readonly List<TextureRect> pieces = new();
	private readonly List<Vector2> pieceStarts = new();
	private PanelContainer plate = null!;
	private Label kicker = null!;
	private Label title = null!;
	private Label effect = null!;

	public override void _Ready()
	{
		Layer = MomentLayer;
		ProcessMode = ProcessModeEnum.Pausable;

		root = new Control { Name = "SetAssembledRoot", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);

		burst = new BurstDrawing { Name = "Burst", MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(burst);

		objectIcon = MakeIcon(ObjectScale);
		objectIcon.Name = "AssembledObject";
		root.AddChild(objectIcon);

		plate = new PanelContainer { Name = "Plate", MouseFilter = Control.MouseFilterEnum.Ignore };
		var face = new StyleBoxFlat
		{
			BgColor = new Color(CodexRows.Stone.Face, 0.92f),
			BorderColor = GoldDeep,
		};
		face.SetBorderWidthAll(2);
		face.SetCornerRadiusAll(6);
		face.ContentMarginLeft = face.ContentMarginRight = 22f;
		face.ContentMarginTop = 12f;
		face.ContentMarginBottom = 14f;
		plate.AddThemeStyleboxOverride("panel", face);
		root.AddChild(plate);

		var lines = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		lines.AddThemeConstantOverride("separation", 4);
		plate.AddChild(lines);

		kicker = MakeLine(ResponsiveLayout.TextRole.Label, CodexRows.Stone.Reward);
		kicker.Text = "Full Set Enchantment";
		lines.AddChild(kicker);
		title = MakeLine(ResponsiveLayout.TextRole.Display, Gold);
		lines.AddChild(title);
		effect = MakeLine(ResponsiveLayout.TextRole.Body, CodexRows.Stone.Ink);
		effect.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		lines.AddChild(effect);

		SetProcess(false);
	}

	/// <summary>Plays the moment for this set, after any already queued.</summary>
	public void Play(ChestSetDefinition set)
	{
		if (set == null)
			return;
		pending.Enqueue(set);
		if (showing == null)
			StartNext();
	}

	private void StartNext()
	{
		if (pending.Count == 0)
		{
			showing = null;
			root.Visible = false;
			SetProcess(false);
			return;
		}

		showing = pending.Dequeue();
		clock = 0f;
		flashed = false;

		objectIcon.Texture = BonelightSkin.LoadTextureSafe(showing.IconPath);
		title.Text = showing.Name;
		effect.Text = showing.Description;

		foreach (TextureRect old in pieces)
			old.QueueFree();
		pieces.Clear();
		foreach (string itemId in showing.RequiredItemIds)
		{
			TextureRect piece = MakeIcon(PieceScale);
			piece.Texture = BonelightSkin.LoadTextureSafe(ChestItemCatalog.GetIconPath(itemId));
			root.AddChild(piece);
			pieces.Add(piece);
		}

		// Shown on the first frame the run is actually running, not now: a set is completed by
		// picking a relic, and at that moment the chest menu is still up over a paused tree.
		root.Visible = false;
		SetProcess(true);
	}

	public override void _Process(double delta)
	{
		if (showing == null)
			return;

		clock += (float)delta;
		root.Visible = true;
		Layout();

		if (!flashed && clock >= GatherSeconds)
		{
			flashed = true;
			SfxPlayer.Global(SfxCatalog.SpellEvolve);
		}

		float fade = clock <= HoldUntil ? 1f : Mathf.Clamp(1f - (clock - HoldUntil) / FadeSeconds, 0f, 1f);
		root.Modulate = new Color(1f, 1f, 1f, fade);
		if (clock >= HoldUntil + FadeSeconds)
			StartNext();
	}

	// Everything is placed from the viewport each frame, so a window resized mid-moment, or the
	// phone's portrait canvas, keeps the object over the player and the plate on screen.
	private void Layout()
	{
		Vector2 view = root.Size;
		bool narrow = ResponsiveLayout.IsNarrow(root);
		float objectSize = IconSource * ObjectScale;
		float pieceSize = IconSource * PieceScale;

		float plateWidth = narrow
			? MathF.Min(NarrowPlateMaxWidth, view.X - SideMargin * 2f)
			: MathF.Min(WidePlateWidth, view.X - objectSize - PlateGap - SideMargin * 2f);
		plate.CustomMinimumSize = new Vector2(plateWidth, 0f);
		effect.CustomMinimumSize = new Vector2(plateWidth - 44f, 0f);
		float plateHeight = plate.GetCombinedMinimumSize().Y;

		float bandTop = narrow ? NarrowHudBand : WideHudBand;
		float bandBottom = view.Y * 0.5f - PlayerHeadroom;
		Vector2 centre;
		Vector2 plateAt;
		if (narrow)
		{
			float stack = objectSize + PlateGap + plateHeight;
			float top = MathF.Max(bandTop, (bandTop + bandBottom - stack) * 0.5f);
			centre = new Vector2(view.X * 0.5f, top + objectSize * 0.5f);
			plateAt = new Vector2((view.X - plateWidth) * 0.5f, top + objectSize + PlateGap);
		}
		else
		{
			float row = objectSize + PlateGap + plateWidth;
			float left = (view.X - row) * 0.5f;
			float middle = MathF.Max(bandTop + objectSize * 0.5f, (bandTop + bandBottom) * 0.5f);
			centre = new Vector2(left + objectSize * 0.5f, middle);
			plateAt = new Vector2(left + objectSize + PlateGap, middle - plateHeight * 0.5f);
		}

		// The pieces start spread on a line through the object, as wide as the screen allows, and
		// meet at its centre with an ease-in: slow to leave, fast to arrive, so they slam together.
		float gather = Mathf.Clamp(clock / GatherSeconds, 0f, 1f);
		float travel = gather * gather;
		float spread = MathF.Min(view.X * 0.5f - pieceSize, 360f);
		for (int i = 0; i < pieces.Count; i++)
		{
			float slot = pieces.Count == 1 ? 0f : (i / (float)(pieces.Count - 1)) * 2f - 1f;
			// Every other piece comes from a little above the line, so three or four of them read as
			// a gathering and not as a row.
			Vector2 start = centre + new Vector2(slot * spread, (i % 2 == 0 ? -1f : 1f) * pieceSize * 0.35f);
			Vector2 at = start.Lerp(centre, travel);
			TextureRect piece = pieces[i];
			piece.Visible = gather < 1f;
			piece.Position = at - new Vector2(pieceSize, pieceSize) * 0.5f;
			piece.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(gather * 3f, 0f, 1f));
		}

		// The object appears at the flash, overshooting a little and settling.
		float sinceFlash = clock - GatherSeconds;
		objectIcon.Visible = sinceFlash >= 0f;
		float pop = sinceFlash < 0f ? 0f : Mathf.Clamp(sinceFlash / PopSeconds, 0f, 1f);
		float objectScale = Mathf.Lerp(1.25f, 1f, pop * pop);
		objectIcon.Size = new Vector2(objectSize, objectSize);
		objectIcon.PivotOffset = objectIcon.Size * 0.5f;
		objectIcon.Scale = new Vector2(objectScale, objectScale);
		// It rises a little as it goes, so the fade reads as the object lifting away.
		float lift = clock > HoldUntil ? (clock - HoldUntil) / FadeSeconds * 24f : 0f;
		objectIcon.Position = centre - objectIcon.Size * 0.5f - new Vector2(0f, lift);

		burst.Position = Vector2.Zero;
		burst.Size = view;
		burst.Centre = centre - new Vector2(0f, lift);
		burst.ObjectRadius = objectSize * 0.5f;
		burst.SinceFlash = sinceFlash;
		burst.Gather = gather;
		burst.QueueRedraw();

		// The plate beside or under the object: its name and what it now does.
		plate.Size = new Vector2(plateWidth, plateHeight);
		plate.Position = plateAt - new Vector2(0f, lift);
		float textIn = sinceFlash < 0f ? 0f : Mathf.Clamp(sinceFlash / TextInSeconds, 0f, 1f);
		plate.Modulate = new Color(1f, 1f, 1f, textIn);
	}

	private static TextureRect MakeIcon(float scale)
	{
		float size = IconSource * scale;
		return new TextureRect
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			CustomMinimumSize = new Vector2(size, size),
			Size = new Vector2(size, size),
		};
	}

	private Label MakeLine(ResponsiveLayout.TextRole role, Color colour)
	{
		var label = new Label
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		// Sized from the node's own viewport, so it has to be in the tree first.
		root.AddChild(label);
		ResponsiveLayout.SetFont(label, role);
		root.RemoveChild(label);
		label.AddThemeColorOverride("font_color", colour);
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
		label.AddThemeConstantOverride("outline_size", 4);
		return label;
	}

	/// <summary>
	/// The light behind the object: a soft glow while the pieces gather, a white flash and a ring
	/// thrown out when they meet, and slow rays for as long as the object stands. Light only - no
	/// disc wide enough to hide the fight under it.
	/// </summary>
	private partial class BurstDrawing : Control
	{
		public Vector2 Centre;
		public float ObjectRadius;
		public float SinceFlash;
		public float Gather;

		private const int Rays = 12;

		public override void _Draw()
		{
			// Gathering: a glow that tightens and brightens as the pieces close in.
			if (SinceFlash < 0f)
			{
				float g = Gather * Gather;
				DrawCircle(Centre, ObjectRadius * Mathf.Lerp(1.6f, 0.9f, g), new Color(Gold, 0.10f + 0.18f * g));
				return;
			}

			// The rays, turning slowly, long and faint; they last as long as the object does.
			float settle = Mathf.Clamp(SinceFlash / 0.35f, 0f, 1f);
			float turn = SinceFlash * 0.35f;
			for (int i = 0; i < Rays; i++)
			{
				float a = turn + i * Mathf.Tau / Rays;
				var dir = Vector2.FromAngle(a);
				var side = new Vector2(-dir.Y, dir.X);
				float length = ObjectRadius * (i % 2 == 0 ? 1.7f : 1.35f) * settle;
				float half = ObjectRadius * 0.09f;
				DrawColoredPolygon(new[]
				{
					Centre + dir * ObjectRadius * 0.4f + side * half,
					Centre + dir * length,
					Centre + dir * ObjectRadius * 0.4f - side * half,
				}, new Color(Gold, 0.22f));
			}

			// The glow the object stands in.
			DrawCircle(Centre, ObjectRadius * 1.05f, new Color(Gold, 0.16f));
			DrawCircle(Centre, ObjectRadius * 0.75f, new Color(Gold, 0.14f));

			// The flash: white, gone in a quarter of a second.
			float flash = 1f - Mathf.Clamp(SinceFlash / 0.25f, 0f, 1f);
			if (flash > 0f)
				DrawCircle(Centre, ObjectRadius * (0.8f + 0.5f * (1f - flash)), new Color(1f, 0.98f, 0.9f, 0.75f * flash));

			// The ring, thrown out once.
			float ring = Mathf.Clamp(SinceFlash / RingSeconds, 0f, 1f);
			if (ring < 1f)
			{
				float eased = 1f - (1f - ring) * (1f - ring);
				DrawArc(Centre, ObjectRadius * Mathf.Lerp(0.6f, 2.2f, eased), 0f, Mathf.Tau, 64,
					new Color(Gold, 0.9f * (1f - ring)), Mathf.Lerp(6f, 1.5f, ring), true);
			}
		}
	}
}
