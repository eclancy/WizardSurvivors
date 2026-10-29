using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

/// <summary>
/// The world map: where the chosen wizard walks between chapters and a run is started
/// (.ai/world-map.md). It replaces the stage list in the flow - CharacterSelection, this, the run.
/// </summary>
/// <remarks>
/// Three parts, each its own control: the drawn world (<see cref="WorldMapView"/>), the compass strip
/// across the top (<see cref="WorldMapCompass"/>), and the card for the selected stop, built here.
/// The stops are the campaign in StageCatalog order, then Boss Rush once it is open, then the
/// chapters outside the campaign (the Sketchbook) - left and right step through them, and every one
/// is visitable, locked or not, so its card can say why (.ai/world-map.md, section 2). A locked stop
/// can never be started: <see cref="TryStart"/> asks the save again at the moment of starting.
/// </remarks>
public partial class WorldMap : Control
{
	/// <summary>The stop a run was last started from, for the session: returning puts him back there.</summary>
	public static string LastStopId { get; set; }

	/// <summary>Where the harness wants him to open (_UiShot --map-at). Read once, then cleared.</summary>
	public static string StartOverride { get; set; }

	private const float CardWidth = 404f;
	private const float NarrowCardHeight = 492f;
	private const float RepeatDelay = 0.40f;
	private const float RepeatInterval = 0.18f;

	private sealed class Stop
	{
		public WorldMapStop Layout { get; init; }
		public StageDefinition Stage { get; init; }
		public bool IsBossRush { get; init; }
		public WorldMapView.StopLook Look { get; set; }
		public bool Cleared { get; set; }
		public string Id => Layout.Id;
	}

	private readonly List<Stop> stops = new();
	private WorldMapLayout layout;
	private WorldMapView view;
	private WorldMapCompass compass;
	private Button backButton;
	private Button startButton;
	private Button leftArrow;
	private Button rightArrow;
	private VBoxContainer cardBox;
	private SaveData save;
	private CharacterData character;
	private bool narrow;
	private int selected;

	// The held-stick repeat (MenuNavigator's rule: the stick and the D-pad, never the keyboard, which
	// has its own repeat; nothing until a direction held as the screen opened is let go).
	private bool heldAtOpen;
	private int heldDirection;
	private float repeatCountdown;

	public override void _Ready()
	{
		layout = WorldMapLayout.Load(out string problem);
		if (layout == null)
		{
			// The art is generated and committed, so this is a broken checkout, not a player's state:
			// say so loudly, and keep the game usable through the old list.
			GD.PushError($"WorldMap: {problem}. Falling back to the stage list.");
			CallDeferred(nameof(FallBack));
			return;
		}

		save = GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data;
		character = CharacterRoster.GetByIndex(Global.SelectedCharacterIdx);
		narrow = ResponsiveLayout.IsNarrow(this);
		BuildStops();

		var ground = new ColorRect { Name = "Ground", Color = new Color("#05070C"), MouseFilter = MouseFilterEnum.Ignore };
		ground.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(ground);

		BuildMapView();
		BuildTopBar();
		BuildCard();

		selected = ResolveStartIndex();
		view.Setup(layout, stops.Select(s => new WorldMapView.ViewStop { Layout = s.Layout, Look = s.Look }).ToList(),
			character, selected);
		compass.SetMarks(stops.Select(MarkFor));
		compass.SetState(selected, selected);
		RebuildCard();

		heldAtOpen = AnyDirectionHeld();

		// Clear of the phone's rounded corners and camera hole; nothing on desktop.
		SafeArea.Apply(this);
		MenuNavigator.Attach(this, startButton);
	}

	private void FallBack() => SceneTransition.ChangeScene(this, "res://scenes/StageSelection.tscn");

	// --- the stops -------------------------------------------------------------------------------

	private void BuildStops()
	{
		var order = new List<(StageDefinition Stage, bool Rush)>();
		foreach (StageDefinition stage in StageCatalog.Campaign)
			order.Add((stage, false));
		if (save != null && BossRushCatalog.IsOpen(save) && BossRushCatalog.Arena != null)
			order.Add((BossRushCatalog.Arena, true));
		foreach (StageDefinition stage in StageCatalog.All.Where(s => !StageCatalog.IsInCampaign(s)))
			order.Add((stage, false));

		foreach (var (stage, rush) in order)
		{
			WorldMapStop placed = layout.Get(rush ? WorldMapLayout.BossRushId : stage.Id);
			if (placed == null)
			{
				// RegressionChecks.ValidateWorldMap names this too; here it only costs the stop.
				GD.PushError($"WorldMap: '{(rush ? WorldMapLayout.BossRushId : stage.Id)}' has no place on the map (tools/art/world_map.py PLACES).");
				continue;
			}
			stops.Add(new Stop
			{
				Layout = placed,
				Stage = stage,
				IsBossRush = rush,
				Look = rush || GlobalStatsManager.IsStageAvailable(save, stage) ? WorldMapView.StopLook.Open
					: !stage.IsPlayable ? WorldMapView.StopLook.Unbuilt
					: WorldMapView.StopLook.Locked,
				Cleared = !rush && IsCleared(stage),
			});
		}
		// The route visits them in this order; keep the list in it whatever the catalog order did.
		stops.Sort((a, b) => a.Layout.ArcLength.CompareTo(b.Layout.ArcLength));
	}

	private bool IsCleared(StageDefinition stage)
	{
		BossDefinition boss = BossCatalog.ForStageIndex(stage.Index);
		return boss != null && save?.Lifetime != null && save.Lifetime.HasDefeatedBoss(boss.Id);
	}

	private WorldMapCompass.Mark MarkFor(Stop stop, int index)
	{
		int position = stop.IsBossRush ? -1 : StageCatalog.CampaignPositionOf(stop.Stage.Id);
		bool outside = !stop.IsBossRush && position < 0;
		return new WorldMapCompass.Mark
		{
			Kind = stop.IsBossRush ? WorldMapCompass.MarkKind.Rush
				: outside ? WorldMapCompass.MarkKind.Page
				: stop.Look != WorldMapView.StopLook.Open ? WorldMapCompass.MarkKind.Locked
				: stop.Cleared ? WorldMapCompass.MarkKind.Cleared
				: WorldMapCompass.MarkKind.Open,
			Caption = position >= 0 ? (position + 1).ToString() : string.Empty,
			BreakBefore = outside && index > 0 && !IsOutside(stops[index - 1]),
		};
	}

	private static bool IsOutside(Stop stop) => !stop.IsBossRush && !StageCatalog.IsInCampaign(stop.Stage);

	private int ResolveStartIndex()
	{
		string want = StartOverride ?? LastStopId;
		StartOverride = null;
		if (string.IsNullOrEmpty(want))
			want = save?.RunTelemetryHistory?.FirstOrDefault()?.StageId;
		int found = stops.FindIndex(s => s.Id.Equals(want ?? string.Empty, StringComparison.OrdinalIgnoreCase));
		if (found >= 0)
			return found;
		// A first visit: the furthest open chapter of the campaign, where the road has got to.
		for (int i = stops.Count - 1; i >= 0; i--)
		{
			if (!stops[i].IsBossRush && !IsOutside(stops[i]) && stops[i].Look == WorldMapView.StopLook.Open)
				return i;
		}
		return 0;
	}

	// --- building the screen ---------------------------------------------------------------------

	private void BuildMapView()
	{
		view = new WorldMapView { Name = "MapView" };
		view.SetAnchorsPreset(LayoutPreset.FullRect);
		if (narrow)
		{
			view.OffsetTop = TopBarHeight() + 16f;
			view.OffsetBottom = -(NarrowCardHeight + 24f);
		}
		else
		{
			view.OffsetLeft = 16f;
			view.OffsetTop = TopBarHeight() + 20f;
			view.OffsetRight = -(CardWidth + 32f);
			view.OffsetBottom = -16f;
		}
		view.StopPressed += SelectStop;
		AddChild(view);

		// The step plates, at the map's sides: on a phone the only way to step, on a PC a mouse's.
		leftArrow = MakeArrow("left", -1);
		rightArrow = MakeArrow("right", 1);
		view.AddChild(leftArrow);
		view.AddChild(rightArrow);
	}

	private Button MakeArrow(string side, int direction)
	{
		var arrow = new Button
		{
			Name = side == "left" ? "StepLeft" : "StepRight",
			FocusMode = FocusModeEnum.None,
			CustomMinimumSize = new Vector2(56f, 96f),
			Icon = BonelightSkin.LoadTextureSafe(WorldMapLayout.Directory + "arrow-" + side + ".png"),
			IconAlignment = HorizontalAlignment.Center,
			ExpandIcon = false,
			TooltipText = direction < 0 ? "Back along the road" : "On along the road",
		};
		BonelightSkin.StyleButton(arrow, BonelightSkin.Emphasis.Quiet);
		arrow.AnchorTop = 0.5f;
		arrow.AnchorBottom = 0.5f;
		arrow.OffsetTop = -48f;
		arrow.OffsetBottom = 48f;
		if (direction < 0)
		{
			arrow.AnchorLeft = arrow.AnchorRight = 0f;
			arrow.OffsetLeft = 8f;
			arrow.OffsetRight = 64f;
		}
		else
		{
			arrow.AnchorLeft = arrow.AnchorRight = 1f;
			arrow.OffsetLeft = -64f;
			arrow.OffsetRight = -8f;
		}
		arrow.Pressed += () => Step(direction);
		return arrow;
	}

	private float TopBarHeight() => narrow ? 80f : 76f;

	private void BuildTopBar()
	{
		var bar = new HBoxContainer { Name = "TopBar" };
		bar.AddThemeConstantOverride("separation", 12);
		bar.AnchorLeft = 0f;
		bar.AnchorRight = 1f;
		bar.AnchorTop = 0f;
		bar.AnchorBottom = 0f;
		bar.OffsetLeft = 16f;
		bar.OffsetRight = -16f;
		bar.OffsetTop = 12f;
		bar.OffsetBottom = 12f + TopBarHeight();
		AddChild(bar);

		backButton = new Button
		{
			Name = "BackButton",
			Text = "Back",
			CustomMinimumSize = new Vector2(narrow ? 112f : 140f, 0f),
			SizeFlagsVertical = SizeFlags.Fill,
		};
		ResponsiveLayout.SetFont(backButton, ResponsiveLayout.TextRole.Body);
		BonelightSkin.StyleButton(backButton, BonelightSkin.Emphasis.Normal);
		backButton.Pressed += OnBack;
		bar.AddChild(backButton);

		compass = new WorldMapCompass
		{
			Name = "Compass",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.Fill,
		};
		compass.MarkPressed += SelectStop;
		bar.AddChild(compass);
	}

	private void BuildCard()
	{
		var card = new PanelContainer { Name = "Card" };
		BonelightSkin.ApplyPanel(card, BonelightSkin.Register.Stone);
		if (narrow)
		{
			card.AnchorLeft = 0f;
			card.AnchorRight = 1f;
			card.AnchorTop = 1f;
			card.AnchorBottom = 1f;
			card.OffsetLeft = 12f;
			card.OffsetRight = -12f;
			card.OffsetTop = -(NarrowCardHeight + 12f);
			card.OffsetBottom = -12f;
		}
		else
		{
			card.AnchorLeft = 1f;
			card.AnchorRight = 1f;
			card.AnchorTop = 0f;
			card.AnchorBottom = 1f;
			card.OffsetLeft = -(CardWidth + 16f);
			card.OffsetRight = -16f;
			card.OffsetTop = TopBarHeight() + 20f;
			card.OffsetBottom = -16f;
		}
		AddChild(card);

		var margin = new MarginContainer();
		foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
			margin.AddThemeConstantOverride(side, 18);
		card.AddChild(margin);

		var column = new VBoxContainer();
		column.AddThemeConstantOverride("separation", 10);
		margin.AddChild(column);

		cardBox = new VBoxContainer { Name = "CardBody", SizeFlagsVertical = SizeFlags.ExpandFill };
		cardBox.AddThemeConstantOverride("separation", 8);
		column.AddChild(cardBox);

		startButton = new Button
		{
			Name = "StartButton",
			CustomMinimumSize = new Vector2(0f, 60f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		ResponsiveLayout.SetFont(startButton, ResponsiveLayout.TextRole.Body);
		BonelightSkin.StyleButton(startButton, BonelightSkin.Emphasis.Primary);
		startButton.Pressed += TryStart;
		column.AddChild(startButton);

		// Left and right move the map, never the selection: every focus stop's sideways neighbour is
		// itself, so neither Godot's step nor MenuNavigator's held-stick repeat can walk it off.
		foreach (Button button in new[] { startButton, backButton })
		{
			button.FocusNeighborLeft = new NodePath(".");
			button.FocusNeighborRight = new NodePath(".");
		}
		startButton.FocusNeighborTop = startButton.GetPathTo(backButton);
		startButton.FocusNeighborBottom = startButton.GetPathTo(backButton);
		backButton.FocusNeighborTop = backButton.GetPathTo(startButton);
		backButton.FocusNeighborBottom = backButton.GetPathTo(startButton);
	}

	// --- the card --------------------------------------------------------------------------------

	private static readonly Color Ink = CodexRows.Stone.Ink;
	private static readonly Color InkSoft = CodexRows.Stone.InkSoft;
	private static readonly Color InkDone = CodexRows.Stone.Done;
	private static readonly Color InkFlag = CodexRows.Stone.Flag;
	// The gold of the inlay, for the "Chapter 3 of 10" line over the name.
	private static readonly Color InkPlace = new(0.85f, 0.69f, 0.29f);

	private void RebuildCard()
	{
		foreach (Node child in cardBox.GetChildren())
		{
			cardBox.RemoveChild(child);
			child.QueueFree();
		}
		if (selected < 0 || selected >= stops.Count)
			return;
		Stop stop = stops[selected];
		StageDefinition stage = stop.Stage;
		bool open = stop.Look == WorldMapView.StopLook.Open;

		int position = stop.IsBossRush ? -1 : StageCatalog.CampaignPositionOf(stage.Id);
		string where = stop.IsBossRush ? "The seat, at the summit"
			: position >= 0 ? $"Chapter {position + 1} of {StageCatalog.Campaign.Count}"
			: "Beyond the map";
		cardBox.AddChild(MakeText(where, ResponsiveLayout.TextRole.Label, InkPlace, "CardWhere"));
		var name = MakeText(stop.IsBossRush ? "Boss Rush" : stage.DisplayName, ResponsiveLayout.TextRole.Title, Ink, "CardName");
		cardBox.AddChild(name);

		string line = stop.IsBossRush ? BossRushLine()
			: string.IsNullOrWhiteSpace(stage.CorruptionText) ? stage.FlavorText : stage.CorruptionText;
		cardBox.AddChild(MakeText(line, ResponsiveLayout.TextRole.Body, InkSoft, "CardLine"));

		if (stop.IsBossRush)
		{
			cardBox.AddChild(GlyphLine("kills", BossRushRecord(), InkSoft, "CardState"));
		}
		else if (!open)
		{
			cardBox.AddChild(GlyphLine("lock", stage.LockedHint, InkFlag, "CardState"));
			if (stage.Gate == StageGate.CampaignComplete && stage.IsPlayable)
			{
				// The one gate the player cannot open by beating one thing, and the one place it is
				// written down with its count (the stage list lost this when it hid closed chapters).
				int spells = GlobalStatsManager.RemainingSpellCount(save);
				int wizards = GlobalStatsManager.RemainingWizardCount(save);
				cardBox.AddChild(MakeText($"Still held: {Plural(spells, "spell")} and {Plural(wizards, "wizard")}",
					ResponsiveLayout.TextRole.Label, InkSoft, "CardGoal"));
			}
		}
		else
		{
			BossDefinition boss = BossCatalog.ForStageIndex(stage.Index);
			if (boss != null)
			{
				cardBox.AddChild(stop.Cleared
					? GlyphLine("done", $"{boss.DisplayName} has fallen", InkDone, "CardState")
					: GlyphLine("elite", $"{boss.DisplayName} holds it", InkSoft, "CardState"));
			}
			float best = save?.Lifetime?.BestTimeOnStage(stage.Id) ?? 0f;
			if (best > 0f)
				cardBox.AddChild(GlyphLine("duration", $"Longest stand: {FormatMinutes(best)}", InkSoft, "CardBest"));
		}

		if (!stop.IsBossRush)
			AddRewards(stage);

		startButton.Text = stop.IsBossRush ? "Begin the Boss Rush"
			: open ? $"Enter {WithArticle(stage.DisplayName)}"
			: "Locked";
		startButton.TooltipText = open || stop.IsBossRush ? string.Empty : stage.LockedHint;
	}

	private void AddRewards(StageDefinition stage)
	{
		IReadOnlyList<StageReward> rewards = StageRewards.ForStage(stage.Id);
		if (rewards.Count == 0)
			return;
		cardBox.AddChild(MakeText("What he keeps here", ResponsiveLayout.TextRole.Label, InkPlace, "CardHoldsHeading"));
		var row = new HFlowContainer { Name = "CardHolds" };
		row.AddThemeConstantOverride("h_separation", 8);
		row.AddThemeConstantOverride("v_separation", 8);
		int found = 0;
		foreach (StageReward reward in rewards)
		{
			bool has = StageRewards.IsFound(save, reward);
			if (has)
				found++;
			row.AddChild(BuildSlot(reward, has));
		}
		cardBox.AddChild(row);
		cardBox.AddChild(MakeText($"{found} of {rewards.Count} found", ResponsiveLayout.TextRole.Label, InkSoft, "CardHoldsCount"));
	}

	private const float SlotBox = 64f;

	private Control BuildSlot(StageReward reward, bool found)
	{
		List<Texture2D> frames = SlotFrames(reward, found, out int scale);
		var slot = new PanelContainer
		{
			Name = $"Slot_{reward.Kind}_{reward.Id}",
			CustomMinimumSize = new Vector2(SlotBox + 8f, SlotBox + 8f),
			MouseFilter = MouseFilterEnum.Pass,
			TooltipText = StageRewards.Describe(save, reward, NameOf),
		};
		var style = new StyleBoxFlat
		{
			BgColor = found ? new Color(0.118f, 0.149f, 0.204f) : new Color(0.043f, 0.059f, 0.094f),
			BorderColor = found ? new Color(0.541f, 0.416f, 0.118f) : new Color(0.165f, 0.204f, 0.267f),
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(4);
		slot.AddThemeStyleboxOverride("panel", style);
		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.AddChild(new PixelArtRect(frames, scale, SlotBox)
		{
			FramesPerSecond = CharacterVisuals.IdleFramesPerSecond,
		});
		slot.AddChild(center);
		return slot;
	}

	/// <summary>
	/// The slot's picture: its own icon or sprite when found, its silhouette when not - the character
	/// select's locked-wizard shadow, for every kind, so a hidden thing reads as one family - and the
	/// sealed portrait for a wizard whose chapter has not opened.
	/// </summary>
	private List<Texture2D> SlotFrames(StageReward reward, bool found, out int scale)
	{
		scale = 1;
		List<Texture2D> art;
		switch (reward.Kind)
		{
			case StageRewardKind.Wizard:
				if (StageRewards.IsSealed(save, reward))
					return new List<Texture2D> { GD.Load<Texture2D>("res://assets/bonelight/characters/sealed-portrait.png") };
				CharacterData who = CharacterRoster.GetAll().FirstOrDefault(c => c.Id.Equals(reward.Id, StringComparison.OrdinalIgnoreCase));
				art = CharacterVisuals.GetIdleFrames(who?.Portrait);
				break;
			case StageRewardKind.Boon:
				art = new List<Texture2D> { BonelightSkin.LoadTextureSafe(BoonCatalog.GetById(reward.Id)?.IconPath) };
				break;
			case StageRewardKind.ArcaneEnergy:
				// A stat glyph, 12 art pixels saved x2: drawn x2 again it sits in the slot like an icon.
				art = new List<Texture2D> { StatIcons.Load("energy") };
				scale = 2;
				break;
			case StageRewardKind.Secret:
				art = new List<Texture2D> { BonelightSkin.LoadTextureSafe(WorldMapLayout.Directory + "secret.png") };
				break;
			case StageRewardKind.Keepsake:
				art = new List<Texture2D> { BonelightSkin.LoadTextureSafe(WorldMapLayout.Directory + "keepsake.png") };
				break;
			default:
				Texture2D icon = SpellIcon(reward.Id);
				art = new List<Texture2D> { icon };
				break;
		}
		art = art.Where(t => t != null).ToList();
		return found ? art : CharacterVisuals.BuildSilhouetteFrames(art);
	}

	private static Texture2D SpellIcon(string spellId)
	{
		SpellData spell = ContentValidator.LoadAllSpells().FirstOrDefault(s => s.Id.Equals(spellId, StringComparison.OrdinalIgnoreCase));
		return spell?.Icon ?? BonelightSkin.LoadTextureSafe("res://assets/bonelight/ui/spells/unknown.png");
	}

	private static string NameOf(StageReward reward) => reward.Kind switch
	{
		StageRewardKind.Wizard => CharacterRoster.GetAll().FirstOrDefault(c => c.Id.Equals(reward.Id, StringComparison.OrdinalIgnoreCase))?.Name ?? reward.Id,
		StageRewardKind.Boon => BoonCatalog.GetById(reward.Id)?.Name ?? reward.Id,
		_ => ContentValidator.LoadAllSpells().FirstOrDefault(s => s.Id.Equals(reward.Id, StringComparison.OrdinalIgnoreCase))?.Name ?? reward.Id,
	};

	private string BossRushLine()
	{
		StageDefinition arena = BossRushCatalog.Arena;
		string where = arena == null ? "the Emberdeep"
			: arena.DisplayName.StartsWith("The ") ? "the " + arena.DisplayName.Substring(4) : arena.DisplayName;
		return $"Every warden of the road, one after another, in {where}. The last is his.";
	}

	private string BossRushRecord()
	{
		int total = BossRushCatalog.Roster.Count;
		if (save == null)
			return $"{total} bosses";
		return save.BossRushBestClearSeconds > 0f ? $"Cleared in {FormatMinutes(save.BossRushBestClearSeconds)}"
			: save.BossRushBestFelled > 0 ? $"Best: {save.BossRushBestFelled} of {total} felled"
			: $"{total} bosses, one after another";
	}

	private static string FormatMinutes(float seconds)
	{
		int whole = Mathf.Max(0, Mathf.RoundToInt(seconds));
		return $"{whole / 60}:{whole % 60:00}";
	}

	private static string Plural(int n, string word) => n == 1 ? $"1 {word}" : $"{n} {word}s";

	// "Enter the Fallow Fields", "Enter Enchanted Forest": a name's own article, lower-cased mid-sentence.
	private static string WithArticle(string name) =>
		name != null && name.StartsWith("The ", StringComparison.Ordinal) ? "the " + name.Substring(4) : name;

	private Label MakeText(string text, ResponsiveLayout.TextRole role, Color ink, string nodeName)
	{
		var label = new Label { Name = nodeName, Text = text, MouseFilter = MouseFilterEnum.Ignore };
		if (role == ResponsiveLayout.TextRole.Title || role == ResponsiveLayout.TextRole.Display)
			ResponsiveLayout.SetFont(label, role);
		else
			ResponsiveLayout.SetBodyText(label, role);
		label.AddThemeColorOverride("font_color", ink);
		return label;
	}

	private Control GlyphLine(string glyph, string text, Color ink, string nodeName)
	{
		var row = new HBoxContainer { Name = nodeName, MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 8);
		Texture2D icon = StatIcons.Load(glyph);
		if (icon != null)
		{
			row.AddChild(new TextureRect
			{
				Texture = icon,
				CustomMinimumSize = new Vector2(24f, 24f),
				StretchMode = TextureRect.StretchModeEnum.KeepCentered,
				TextureFilter = TextureFilterEnum.Nearest,
				SizeFlagsVertical = SizeFlags.ShrinkBegin,
				MouseFilter = MouseFilterEnum.Ignore,
			});
		}
		Label label = MakeText(text, ResponsiveLayout.TextRole.Label, ink, nodeName + "Text");
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(label);
		return row;
	}

	// --- moving ----------------------------------------------------------------------------------

	private void Step(int direction)
	{
		if (stops.Count == 0)
			return;
		SelectStop(Mathf.Clamp(selected + direction, 0, stops.Count - 1));
	}

	private void SelectStop(int index)
	{
		if (index < 0 || index >= stops.Count || index == selected)
			return;
		selected = index;
		view.Select(index);
		RebuildCard();
		SfxPlayer.Global(SfxCatalog.UiHover);
	}

	public override void _Input(InputEvent @event)
	{
		if (view == null || !IsVisibleInTree())
			return;
		// Heard before Godot's own focus step, and consumed, so left and right move the map and never
		// the selection. Echoes included: a held arrow key walks on at the keyboard's own repeat.
		if (@event.IsActionPressed("ui_left", true))
		{
			Step(-1);
			GetViewport().SetInputAsHandled();
		}
		else if (@event.IsActionPressed("ui_right", true))
		{
			Step(1);
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (view != null && @event.IsActionPressed("ui_cancel"))
		{
			GetViewport().SetInputAsHandled();
			OnBack();
		}
	}

	public override void _Notification(int what)
	{
		// Android's back button.
		if (what == NotificationWMGoBackRequest && view != null)
			OnBack();
	}

	public override void _Process(double delta)
	{
		if (view == null)
			return;
		compass.SetState(selected, view.WizardStopProgress);
		UpdateRepeat((float)delta);
	}

	private void UpdateRepeat(float delta)
	{
		int held = HeldPadDirection();
		if (heldAtOpen)
		{
			if (held == 0 && !AnyDirectionHeld())
				heldAtOpen = false;
			return;
		}
		if (held == 0)
		{
			heldDirection = 0;
			return;
		}
		if (held != heldDirection)
		{
			// The first step was the action event's; repeats start after the delay.
			heldDirection = held;
			repeatCountdown = RepeatDelay;
			return;
		}
		repeatCountdown -= delta;
		if (repeatCountdown > 0f)
			return;
		repeatCountdown = RepeatInterval;
		Step(held);
	}

	/// <summary>The stick or the D-pad held left (-1) or right (1). The hardware, not the actions:
	/// the keyboard has its own repeat and the two would stack.</summary>
	private static int HeldPadDirection()
	{
		foreach (int device in Input.GetConnectedJoypads())
		{
			float x = Input.GetJoyAxis(device, JoyAxis.LeftX);
			if (x <= -0.5f || Input.IsJoyButtonPressed(device, JoyButton.DpadLeft))
				return -1;
			if (x >= 0.5f || Input.IsJoyButtonPressed(device, JoyButton.DpadRight))
				return 1;
		}
		return 0;
	}

	private static bool AnyDirectionHeld()
	{
		foreach (string action in new[] { "ui_left", "ui_right", "move_left", "move_right" })
		{
			if (InputMap.HasAction(action) && Input.IsActionPressed(action))
				return true;
		}
		return HeldPadDirection() != 0;
	}

	// --- leaving ---------------------------------------------------------------------------------

	/// <summary>Starts the selected stop - or refuses, for a place he still holds.</summary>
	private void TryStart()
	{
		if (selected < 0 || selected >= stops.Count)
			return;
		Stop stop = stops[selected];
		if (stop.IsBossRush)
		{
			StageDefinition arena = BossRushCatalog.Arena;
			if (arena == null || !BossRushCatalog.IsOpen(save))
			{
				Refuse();
				return;
			}
			LastStopId = stop.Id;
			Global.SelectedStageIdx = arena.Index;
			Global.BossRush = true;
			SceneTransition.ChangeScene(this, "res://scenes/node_2d_game.tscn", "Boss Rush");
			return;
		}
		// Asked again here, whatever the card said: a locked place can never be started.
		if (!GlobalStatsManager.IsStageAvailable(save, stop.Stage))
		{
			Refuse();
			return;
		}
		LastStopId = stop.Id;
		Global.SelectedStageIdx = stop.Stage.Index;
		Global.BossRush = false;
		SceneTransition.ChangeScene(this, "res://scenes/node_2d_game.tscn", stop.Stage.DisplayName);
	}

	private void Refuse()
	{
		SfxPlayer.Global(SfxCatalog.UiDenied);
		view.Refuse();
	}

	private void OnBack() => SceneTransition.ChangeScene(this, "res://scenes/CharacterSelection.tscn");

	// --- for the harness -------------------------------------------------------------------------

	public int SelectedIndex => selected;
	public string SelectedStopId => selected >= 0 && selected < stops.Count ? stops[selected].Id : string.Empty;
	public WorldMapView View => view;
	public IReadOnlyList<string> StopIds => stops.Select(s => s.Id).ToList();

	/// <summary>Selects a stop by id (the harness's --map-at, after the screen is up).</summary>
	public void SelectById(string id)
	{
		int index = stops.FindIndex(s => s.Id.Equals(id ?? string.Empty, StringComparison.OrdinalIgnoreCase));
		if (index >= 0)
			SelectStop(index);
	}

	/// <summary>Presses Start through its real handler.</summary>
	public void PressStart() => TryStart();
}
