using Godot;
using WizardSurvivors.scripts;

// A CHAPTER'S OWN RULES (.ai/endgame-castle.md 1.9).
//
// Every chapter on the road is the same run on different ground: survive fifteen minutes, kill the boss,
// walk into the portal. The castle's are not - the bridge is travelled under a collapsing span, the Court
// is explored, the Throne is a build and a procession - and each of those needs Node2DGame to ask the
// chapter something at a few fixed points. Writing each chapter's answer into Node2DGame would put four
// agents in one 7,000-line file. So Node2DGame asks THIS, at seven hook points, and a chapter answers by
// subclassing it in a file of its own (BridgeRules.cs, CourtRules.cs, ThroneRules.cs) and filling in its
// one line in ChapterRulesCatalog.
//
// Every hook's default is "no opinion", and a chapter with no rules has none at all (the catalog returns
// null and Node2DGame never creates one), so every existing chapter plays exactly as it did.
//
// It is a Node, added under the run at the start of Node2DGame._Ready - before the terrain, the side event
// and the secrets are placed, because they ask hook 3 - so a chapter's rules can _Process, add children
// (the bridge's collapsing front, the Court's rooms) and free them with the run. Its _Ready runs then,
// with nothing built yet; the RUN START hook is the one that runs once the stage exists.
//
// THE SEVEN HOOKS, and where Node2DGame asks each:
//   1. RunStarted          the end of _Ready: the terrain's first chunks are loaded, the HUD is built.
//   2. TryPickSpawnPoint   every candidate spawn point (GetRandomSpawnPositionAroundPlayer): the trickle,
//                          elites, the miniboss, recycled enemies, and a formation member that misses.
//                          The point is still vetted as any other is (walls, water, a long walk round,
//                          hook 3, separation from other enemies).
//   3. IsPlacementBlocked  IsImpassablePosition: nothing is placed there - no enemy, chest, event site,
//                          secret, the boss or the way home. It does not move or kill what is already there.
//   4. BossSpawnPoint      SpawnBoss: where the chapter's boss appears, instead of near the player.
//   5. VictoryPortalPoint  OpenVictoryPortal: where the way home opens when the boss falls.
//   6. PinnedPaceSeconds   PaceSeconds: the pace clock held at a fixed point (the Throne's end state). The
//                          survival clock and the boss's arrival still read real time (timeElapsed).
//   7. CreateObjective     once, after RunStarted: a control for the objective line under the run timer,
//                          in the slot a side event's line uses - no castle chapter has a side event.
//
// What the hooks are NOT for: a chapter's tuning (pace, XP, the enemy cap: .ai/run-pacing.md is the
// authority and the castle's map chapters are ordinary runs), its art (its own generators and files) or its
// cast (StageBestiary.<Chapter>.cs). If a chapter needs an eighth hook, add it here with a no-op default and
// one call in Node2DGame, in one commit that says why - never a branch on the stage id in Node2DGame.
public abstract partial class ChapterRules : Node
{
	/// <summary>The run. Set by Node2DGame before the rules enter the tree.</summary>
	public Node2DGame Game { get; set; }

	/// <summary>The chapter these rules are for. Set by Node2DGame before the rules enter the tree.</summary>
	public StageDefinition Stage { get; set; }

	/// <summary>
	/// Hook 1, RUN START: once, at the end of Node2DGame._Ready, with the first chunks of terrain loaded,
	/// the player placed at the stage origin and the HUD built. Place the chapter's set pieces here.
	/// </summary>
	public virtual void RunStarted() { }

	/// <summary>
	/// Hook 2, CHOOSING A SPAWN POINT: return true with a point to use it as this candidate instead of the
	/// usual ring round the player (the bridge: ahead along the deck, behind but ahead of the front, at the
	/// parapets). Asked once per candidate, so a rules object that wants a split (65/15/20) rolls it here.
	/// The point should be off screen; Node2DGame still rejects it if it is blocked or a long walk round.
	/// </summary>
	public virtual bool TryPickSpawnPoint(Vector2 playerPosition, out Vector2 point)
	{
		point = default;
		return false;
	}

	/// <summary>
	/// Hook 3, CLOSED TO PLACEMENT: true where nothing may be put down (the fallen deck behind the bridge's
	/// front). Asked very often - by every spawn, chest and placement - so keep it cheap: no allocation, no
	/// tree walk.
	/// </summary>
	public virtual bool IsPlacementBlocked(Vector2 point) => false;

	/// <summary>
	/// Hook 4, WHERE THE BOSS APPEARS: a world point (the bridge's gate, a room of the Court), or null for
	/// the usual, a few hundred pixels from the player.
	/// </summary>
	public virtual Vector2? BossSpawnPoint(BossDefinition boss) => null;

	/// <summary>
	/// Hook 5, WHERE THE WAY HOME OPENS: a world point (the castle gate, the throne room doors), or null for
	/// the usual, just past where the boss fell. The arcane portal opens there and walking into it ends the
	/// run in victory, as on every chapter; a chapter whose way home is drawn draws it at the same point.
	/// </summary>
	public virtual Vector2? VictoryPortalPoint(Vector2 bossFellAt) => null;

	/// <summary>
	/// Hook 6, A PINNED PACE CLOCK: the pace clock's value for the whole run (the Throne pins it at
	/// Node2DGame.ReferenceRunSeconds, the world's end state from the first second), or null for the real
	/// one. Read many times a frame: return a stored value.
	/// </summary>
	public virtual float? PinnedPaceSeconds => null;

	/// <summary>
	/// Hook 7, THE OBJECTIVE LINE: a control for the line under the run timer (the bridge's strip: the
	/// player, the front and the gate on one line), or null for none. Called once, after
	/// <see cref="RunStarted"/>. Node2DGame anchors it across the HUD in <see cref="ObjectiveTop"/> to
	/// <see cref="ObjectiveBottom"/>, insets it from a phone's corners and frees it with the run; the rules
	/// keep the reference and update it (<see cref="ObjectiveLabel"/> makes a text one in the side event's
	/// style).
	/// </summary>
	public virtual Control CreateObjective() => null;

	/// <summary>The objective line's slot, under the boss bar's (44-78), as a side event's line sits.</summary>
	public const float ObjectiveTop = 84f;

	/// <summary>See <see cref="ObjectiveTop"/>.</summary>
	public const float ObjectiveBottom = 112f;

	/// <summary>A text objective, styled as a side event's line is (SideEvent): small, gold, outlined.</summary>
	protected static Label ObjectiveLabel(string text)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		ResponsiveLayout.SetFont(label, ResponsiveLayout.TextRole.Micro);
		label.AddThemeColorOverride("font_color", new Color(0.96f, 0.86f, 0.55f));
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
		label.AddThemeConstantOverride("outline_size", 5);
		return label;
	}
}
