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

	public override void _Ready()
	{
		foreach (string arg in OS.GetCmdlineArgs())
		{
			if (arg.StartsWith("--screen="))
				Screen = arg.Substring("--screen=".Length);
			else if (arg.StartsWith("--settle="))
				SettleFrames = int.Parse(arg.Substring("--settle=".Length));
		}

		var packed = ResourceLoader.Load<PackedScene>("res://scenes/LevelUpMenu.tscn");
		if (packed == null)
		{
			GD.PrintErr("_LevelUpProbe: scenes/LevelUpMenu.tscn did not load");
			GetTree().Quit(1);
			return;
		}

		// INTO THE MAIN TREE, and captured from the root viewport.
		//
		// This probe used to render into a SubViewport because reading the root viewport came back
		// as a flat fill of the clear colour, and it eventually gave up on pixels and reported
		// geometry instead. Both were the same misdiagnosis: the capture was happening before the
		// frame was drawn. Waiting on RenderingServer.FramePostDraw reads the root viewport fine,
		// which scripts/_UiShot.cs now relies on for every menu in the game - and a CanvasLayer menu
		// like this one draws into the root viewport but NOT into a SubViewport, which is why the
		// SubViewport route was never going to work however long it waited.
		menu = packed.Instantiate<LevelUpMenu>();
		// Added to the TREE ROOT, which is where Node2DGame puts it, rather than under this probe
		// node. A CanvasLayer parented below another node still draws, but it did not appear in the
		// captured viewport texture - the capture came back as nothing but the full-screen dim.
		// Mirroring the real parenting is cheaper than working out why.
		AddChild(menu);
		// The scene ships hidden - Node2DGame calls Show() after SetOptions.
		menu.Show();
		menu.SetOptions(Screen == "evolution" ? EvolutionOptions() : SampleOptions(),
			rerollsRemaining: 2,
			equippedSpells: SampleEquipped(), baselineElementCounts: SampleBaseline());
		RenderingServer.FramePostDraw += OnFramePostDraw;
	}

	// Subscribing to FramePostDraw and COUNTING frames, rather than awaiting it once.
	//
	// A single await fires on the post-draw of whichever frame happens to be in flight, which on
	// this menu is one that has not composited the CanvasLayer yet - the capture came back as a
	// flat grey fill, which looks exactly like "the menu did not render" and is why this probe
	// previously concluded pixels were impossible here. scripts/_UiShot.cs counts frames for the
	// same reason, and every menu in the game is photographed that way now.
	public override void _ExitTree()
	{
		RenderingServer.FramePostDraw -= OnFramePostDraw;
	}

	/// <summary>True if every pixel sampled is the same colour - i.e. nothing drew.</summary>
	private static bool IsFlat(Image image)
	{
		Color first = image.GetPixel(0, 0);
		for (int y = 0; y < image.GetHeight(); y += 17)
		{
			for (int x = 0; x < image.GetWidth(); x += 17)
			{
				if (!image.GetPixel(x, y).IsEqualApprox(first))
					return false;
			}
		}
		return true;
	}

	private int drawn;
	private bool captured;
	private bool pressed;

	private void OnFramePostDraw()
	{
		if (captured)
			return;
		drawn++;

		// Press through to the mutation list halfway, then let it settle again.
		if (Screen == "evolution" && !pressed && drawn >= SettleFrames)
		{
			pressed = true;
			Button card = FirstOptionCard(menu);
			if (card == null)
			{
				GD.PrintErr("_LevelUpProbe: no option card found to press");
				GetTree().Quit(1);
				return;
			}
			card.EmitSignal(BaseButton.SignalName.Pressed);
			return;
		}

		// The ascension browser is reached the same way a player reaches it: by pressing the button
		// in the corner. _UiShot cannot do this one - it presses in _Ready, and in a real run this
		// menu does not exist until the player levels.
		if (Screen == "ascension" && !pressed && drawn >= SettleFrames)
		{
			pressed = true;
			Button open = FindNamedButton(menu, "AscensionsButton");
			if (open == null)
			{
				GD.PrintErr("_LevelUpProbe: no AscensionsButton on the level-up screen");
				GetTree().Quit(1);
				return;
			}
			open.EmitSignal(BaseButton.SignalName.Pressed);
			return;
		}

		bool twoStage = Screen == "evolution" || Screen == "ascension";
		if (drawn < SettleFrames * (twoStage ? 3 : 2))
			return;
		captured = true;

		// The capture is attempted and then CHECKED, because a silent blank is worse than no file.
		//
		// scripts/_UiShot.cs photographs every other screen in the game this way, but this menu is
		// a CanvasLayer that will not surrender its pixels to a root-viewport read on this machine
		// - it comes back as a single flat fill of the clear colour whether it is parented under
		// this probe, under the tree root, or instanced by _UiShot directly. Rather than ship a
		// grey rectangle that looks like "the menu did not render", the probe says so, and the
		// geometry report below is what this harness is actually for.
		Image image = GetViewport()?.GetTexture()?.GetImage();
		string png = string.Format("user://levelup-probe-{0}.png", Screen);
		if (image != null && image.SavePng(png) == Error.Ok)
		{
			GD.Print(string.Format("_LevelUpProbe: {0}{1}", ProjectSettings.GlobalizePath(png),
				IsFlat(image) ? "  (LOOKS FLAT - nothing composited)" : ""));
		}
		else
		{
			GD.Print("_LevelUpProbe: capture failed; geometry report only");
		}

		// The geometry report stays. It is stricter than a picture for the thing it measures: a
		// clipped label is a number here and a guess in a screenshot.
		var report = new List<string>();
		Measure(menu, report);
		string path = string.Format("user://levelup-probe-{0}.tsv", Screen);
		using (FileAccess f = FileAccess.Open(path, FileAccess.ModeFlags.Write))
		{
			f.StoreLine("kind	font	x	y	w	h	clipped	text");
			foreach (string line in report)
				f.StoreLine(line);
		}
		GD.Print(string.Format("_LevelUpProbe: {0} controls measured", report.Count));
		GetTree().Quit(0);
	}

	// Every Label and Button under the menu, with the one fact that matters for each: how big
	// its type is, and whether the box it was given is big enough for the text in it.
	// The option cards are Buttons with no Text - their content is child labels - so they are
	// found by elimination rather than by name: the only NAMED buttons on this screen are Reroll
	// and Skip.
	private static Button FindNamedButton(Node n, string name)
	{
		if (n is Button b && b.Name == name)
			return b;
		foreach (Node kid in n.GetChildren())
		{
			Button found = FindNamedButton(kid, name);
			if (found != null)
				return found;
		}
		return null;
	}

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
			new EquippedSpellInfo
			{
				Id = "fireball", DisplayName = "Fireball", CurrentLevel = 5,
				Ascensions = new List<AscensionInfo>
				{
					// Short on levels only.
					new AscensionInfo
					{
						Id = "fb_sun", DisplayName = "Second Sun",
						Description = "The fireball splits into three smaller suns on impact.",
						UnmetRequirements = new List<string> { "Spell level 8 (5/8)" },
					},
					// Short on both, which is the case a one-line lock reason used to hide.
					new AscensionInfo
					{
						Id = "fb_pyre", DisplayName = "Standing Pyre",
						Description = "Leaves a burning pillar where it lands.",
						UnmetRequirements = new List<string> { "Spell level 8 (5/8)", "Requires 4 Fire (2/4)" },
					},
				},
			},
			new EquippedSpellInfo { Id = "cone_of_cold", DisplayName = "Cone of Cold", CurrentLevel = 3 },
			new EquippedSpellInfo
			{
				Id = "magic_missile", DisplayName = "Magic Missile", CurrentLevel = 8,
				Ascensions = new List<AscensionInfo>
				{
					new AscensionInfo
					{
						Id = "mm_swarm", DisplayName = "Starling Swarm",
						Description = "Every missile splits once, and the splits seek separately.",
					},
					new AscensionInfo
					{
						Id = "mm_lance", DisplayName = "Aether Lance",
						Description = "One missile instead of many, and it passes through everything.",
						UnmetRequirements = new List<string> { "Requires 4 Arcane (2/4)" },
					},
				},
			},
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
