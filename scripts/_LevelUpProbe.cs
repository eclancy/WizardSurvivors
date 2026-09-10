using Godot;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// A harness for LOOKING AT the level-up menu without playing to level 2.
//
// Underscore-prefixed and paired with scenes/_LevelUpProbe.tscn, matching _KiteProbe: these are
// developer scenes, not content. Nothing in the game references them.
//
// Why it exists: LevelUpMenu builds every card in code, so its layout cannot be judged from the
// editor, and there is no way to reach it except by earning a level in a real run. That made
// "increase the text size" a change nobody could verify - the reason the menu drifted to thirteen
// different font sizes and a floor of 10 in the first place.
//
// Run it:
//   "$GODOT_BIN" --path . scenes/_LevelUpProbe.tscn
// It writes user://levelup-probe-<screen>.png and quits. Not headless - headless has no renderer,
// so the capture comes back blank.
public partial class _LevelUpProbe : Node
{
	// Which screen to capture. The three are laid out by completely different builders, so all
	// three have to be looked at: BuildWideOptionCard/BuildNarrowOptionCard for the picks,
	// BuildEvolutionOptionCard/BuildNarrowEvolutionCard for the mutations, and
	// BuildEraseSpellCard for the swap.
	[Export] public string Screen { get; set; } = "options";
	[Export] public int SettleFrames { get; set; } = 12;

	private LevelUpMenu menu;
	private SubViewport shot;

	public override void _Ready()
	{
		var packed = ResourceLoader.Load<PackedScene>("res://scenes/LevelUpMenu.tscn");
		if (packed == null)
		{
			GD.PrintErr("_LevelUpProbe: scenes/LevelUpMenu.tscn did not load");
			GetTree().Quit(1);
			return;
		}

		// INTO A SUBVIEWPORT, not the main window. Reading the root viewport's texture on this
		// machine comes back as a flat fill of the clear colour whichever renderer is used and
		// however long the capture waits - the window's backbuffer is not readable here. A
		// SubViewport renders offscreen into a texture we own, which does not care whether a window
		// is composited at all. It also pins the capture to the project's authored 720x1280 rather
		// than to whatever size the window happened to open at.
		shot = new SubViewport
		{
			Size = new Vector2I(720, 1280),
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
			RenderTargetClearMode = SubViewport.ClearMode.Always,
			TransparentBg = false,
		};
		AddChild(shot);

		menu = packed.Instantiate<LevelUpMenu>();
		shot.AddChild(menu);
		// The scene ships hidden - Node2DGame calls Show() after SetOptions. Without this the
		// capture comes back as a flat grey clear colour, which is what the first run of this
		// probe produced.
		menu.Show();
		menu.SetOptions(Screen == "evolution" ? EvolutionOptions() : SampleOptions(),
			rerollsRemaining: 2,
			equippedSpells: SampleEquipped(), baselineElementCounts: SampleBaseline());
		Capture();
	}

	private async void Capture()
	{
		// TWO waits, and both are load-bearing.
		//
		// Process frames first: the menu builds its cards inside SetOptions, but containers do not
		// have their final sizes until layout has run, and the fantasy skin applies its stylebox a
		// frame later still.
		for (int i = 0; i < SettleFrames; i++)
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		if (Screen == "evolution")
		{
			Button card = FirstOptionCard(menu);
			if (card == null)
			{
				GD.PrintErr("_LevelUpProbe: no option card found to press");
				GetTree().Quit(1);
				return;
			}
			card.EmitSignal(BaseButton.SignalName.Pressed);
			for (int i = 0; i < SettleFrames; i++)
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		// Then frame_post_draw, which is the one that actually matters. Reading the viewport
		// texture from _Process reads it BEFORE the frame has been drawn, and comes back as a flat
		// fill of the clear colour - one distinct colour in the whole PNG. That is what the first
		// two runs of this probe produced, and it looks exactly like "the menu did not render".
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

		// GEOMETRY, NOT PIXELS. Capturing this menu as an image does not work on this machine:
		// reading the root viewport comes back as a flat fill of the clear colour under both
		// renderers, and a SubViewport clears but the CanvasLayer will not draw into it. Rather
		// than keep chasing that, the probe reports what it can measure exactly - every label's
		// text, font size and rect - which is what a legibility pass actually needs. It is also
		// stricter than a screenshot: a clipped label is a number here and a guess in a picture.
		var report = new List<string>();
		Measure(menu, report);
		string path = string.Format("user://levelup-probe-{0}.tsv", Screen);
		using (FileAccess f = FileAccess.Open(path, FileAccess.ModeFlags.Write))
		{
			f.StoreLine("kind	font	x	y	w	h	clipped	text");
			foreach (string line in report)
				f.StoreLine(line);
		}
		GD.Print(string.Format("_LevelUpProbe: {0} controls -> {1}",
			report.Count, ProjectSettings.GlobalizePath(path)));
		GetTree().Quit(0);
	}

	// Every Label and Button under the menu, with the one fact that matters for each: how big
	// its type is, and whether the box it was given is big enough for the text in it.
	// The option cards are Buttons with no Text - their content is child labels - so they are
	// found by elimination rather than by name: the only NAMED buttons on this screen are Reroll
	// and Skip.
	private static Button FirstOptionCard(Node n)
	{
		if (n is Button b && string.IsNullOrWhiteSpace(b.Text))
			return b;
		foreach (Node kid in n.GetChildren())
		{
			Button found = FirstOptionCard(kid);
			if (found != null)
				return found;
		}
		return null;
	}

	private static void Measure(Node n, List<string> into)
	{
		if (n is Label lb && !string.IsNullOrWhiteSpace(lb.Text))
		{
			Vector2 want = lb.GetMinimumSize();
			bool clipped = want.X > lb.Size.X + 0.5f || want.Y > lb.Size.Y + 0.5f;
			into.Add(string.Format("Label	{0}	{1}	{2}	{3}	{4}	{5}	{6}",
				lb.GetThemeFontSize("font_size"), (int)lb.GlobalPosition.X, (int)lb.GlobalPosition.Y,
				(int)lb.Size.X, (int)lb.Size.Y, clipped ? 1 : 0,
				lb.Text));
		}
		else if (n is Button bt && !string.IsNullOrWhiteSpace(bt.Text))
		{
			into.Add(string.Format("Button	{0}	{1}	{2}	{3}	{4}	0	{5}",
				bt.GetThemeFontSize("font_size"), (int)bt.GlobalPosition.X, (int)bt.GlobalPosition.Y,
				(int)bt.Size.X, (int)bt.Size.Y, bt.Text));
		}
		else if (n is PanelContainer pc)
		{
			into.Add(string.Format("Card	0	{0}	{1}	{2}	{3}	0	",
				(int)pc.GlobalPosition.X, (int)pc.GlobalPosition.Y, (int)pc.Size.X, (int)pc.Size.Y));
		}
		foreach (Node kid in n.GetChildren())
			Measure(kid, into);
	}

	private static void DumpTree(Node n, int depth)
	{
		string pad = new string(' ', depth * 2);
		string info = n.GetType().Name;
		if (n is Control c)
			info += string.Format(" vis={0} rect={1} size={2}", c.Visible, c.GlobalPosition, c.Size);
		else if (n is CanvasLayer cl)
			info += string.Format(" vis={0} layer={1}", cl.Visible, cl.Layer);
		GD.Print(string.Format("  TREE {0}{1}: {2}", pad, n.Name, info));
		if (depth < 3)
			foreach (Node kid in n.GetChildren())
				DumpTree(kid, depth + 1);
	}

	// Deliberately awkward content, not flattering content. The longest real spell description
	// and a four-element tag set are what actually break a card layout, so those are what the
	// probe shows; a card that only holds "Fireball / +10% damage" proves nothing.
	private static List<LevelUpOption> SampleOptions()
	{
		return new List<LevelUpOption>
		{
			new LevelUpOption
			{
				SpellId = "chain_lightning", DisplayName = "Chain Lightning",
				Description = "Arcs from the first enemy it strikes to the next nearest, "
					+ "losing a little damage with every jump.",
				UpgradeSummary = "+1 jump, +12% damage per jump",
				NextLevel = 3, MaxLevel = 8, IsNewUnlock = false,
				SpellElementTags = new Dictionary<string, int> { { "Lightning", 2 }, { "Arcane", 1 } },
				ElementContribution = new Dictionary<string, int> { { "Lightning", 1 } },
				ResultingElementCounts = new Dictionary<string, int> { { "Lightning", 4 }, { "Arcane", 2 } },
			},
			new LevelUpOption
			{
				SpellId = "black_tentacles", DisplayName = "Black Tentacles",
				Description = "A patch of grasping limbs erupts underfoot and holds everything "
					+ "inside it in place while they squeeze.",
				UpgradeSummary = "Area +20%, root duration +0.4s",
				NextLevel = 1, MaxLevel = 8, IsNewUnlock = true, RequiresSlotSwap = true,
				SpellElementTags = new Dictionary<string, int>
					{ { "Darkness", 2 }, { "Earth", 1 }, { "Poison", 1 } },
				ElementContribution = new Dictionary<string, int> { { "Darkness", 2 }, { "Earth", 1 } },
				ResultingElementCounts = new Dictionary<string, int>
					{ { "Darkness", 3 }, { "Earth", 2 }, { "Poison", 1 } },
			},
			new LevelUpOption
			{
				SpellId = "stone_bulwark", DisplayName = "Stone Bulwark",
				Description = "Reduces every hit by a flat amount before shields are touched.",
				UpgradeSummary = "-1 damage taken per hit",
				NextLevel = 2, MaxLevel = 5, IsNewUnlock = false, IsPassive = true,
				SpellElementTags = new Dictionary<string, int> { { "Earth", 2 } },
				ElementContribution = new Dictionary<string, int> { { "Earth", 1 } },
				ResultingElementCounts = new Dictionary<string, int> { { "Earth", 3 } },
			},
		};
	}

	// The level-4 and level-8 mutation screen, which is laid out by an entirely different pair of
	// builders (BuildEvolutionOptionCard / BuildNarrowEvolutionCard) and therefore has to be
	// measured separately - applying a type scale to the pick cards proves nothing about these.
	private static List<LevelUpOption> EvolutionOptions()
	{
		var forked = new SpellEvolutionOption
		{
			Id = "chain_fork", DisplayName = "Forked Conduit", MilestoneLevel = 4,
			Description = "Every arc splits once more before it dies, at the cost of a longer wind-up "
				+ "between casts.",
			SynergyTag = "Lightning / Chain",
			SynergyDescription = "Deepens Lightning - keeps the spell attuned and pushes its tier.",
			ChainArcBonus = 2, CooldownMultiplier = 1.15f,
		};
		var storm = new SpellEvolutionOption
		{
			Id = "chain_storm", DisplayName = "Gathering Storm", MilestoneLevel = 4,
			Description = "Arcs no longer weaken as they jump, but each one now needs a target within "
				+ "half the usual reach.",
			SynergyTag = "Lightning / Wind",
			SynergyDescription = "Trades attunement for a second element.",
			DamageMultiplier = 1.25f, RangeBonus = -40f,
		};

		var option = new LevelUpOption
		{
			SpellId = "chain_lightning", DisplayName = "Chain Lightning",
			Description = "Arcs from the first enemy it strikes to the next nearest.",
			NextLevel = 4, MaxLevel = 8, IsNewUnlock = false,
			IsEvolutionMilestone = true, MilestoneLevel = 4,
			EvolutionChoices = new List<SpellEvolutionOption> { forked, storm },
			SpellElementTags = new Dictionary<string, int> { { "Lightning", 2 }, { "Arcane", 1 } },
		};
		return new List<LevelUpOption> { option };
	}

	private static List<EquippedSpellInfo> SampleEquipped()
	{
		return new List<EquippedSpellInfo>
		{
			new EquippedSpellInfo { Id = "fireball", DisplayName = "Fireball", CurrentLevel = 5 },
			new EquippedSpellInfo { Id = "cone_of_cold", DisplayName = "Cone of Cold", CurrentLevel = 3 },
			new EquippedSpellInfo { Id = "magic_missile", DisplayName = "Magic Missile", CurrentLevel = 8 },
			new EquippedSpellInfo { Id = "aegis_ward", DisplayName = "Aegis Ward", CurrentLevel = 2, IsPassive = true },
			new EquippedSpellInfo { Id = "blur", DisplayName = "Blur", CurrentLevel = 1, IsPassive = true },
			new EquippedSpellInfo { Id = "obsidian_spike", DisplayName = "Obsidian Spike", CurrentLevel = 4 },
		};
	}

	private static Dictionary<string, int> SampleBaseline()
	{
		return new Dictionary<string, int>
		{
			{ "Lightning", 3 }, { "Arcane", 2 }, { "Darkness", 1 }, { "Earth", 2 }, { "Fire", 4 },
		};
	}
}
