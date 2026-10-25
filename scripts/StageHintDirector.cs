using Godot;
using WizardSurvivors.scripts;

// Plays the chapter's hint (StageHintCatalog; .ai/side-events.md, "Hints that something is out
// there"): partway through a run, while the chapter's side event still holds a wizard or a spell
// this save does not own, a sound from its direction and a caption at that edge of the screen.
//
// Created by SideEventDirector beside the event it is about, so it exists only on a chapter that
// has both an event and a hint, and it reads everything it needs off that event: where it is, whether
// the player has found it, and the colour of its far tell.
//
// WHEN, AND WHEN NOT. Due on the pace clock (first a third of the way in, then every two and a half
// minutes, none from 13:30), and only:
//   - while the event is dormant - nobody has arrived, so its announcement and objective have not
//     taken over;
//   - while none of its sites is marked - seen once on screen, or revealed by the wayfinder. A marked
//     site has a point-of-interest arrow at the screen edge (PoiPointer), which says where it is
//     better than a voice can, so the hints step aside for good. They do not duplicate the arrows;
//     they come BEFORE them, for the player who has not found the wayfinder or seen the light;
//   - while the save still lacks something the site gives (DiscoveryRewards.IsSiteExhausted). Once
//     Tituba is free, nobody calls from the cell again;
//   - never during the boss, and never once the run is over. A caption up when the boss arrives is
//     dismissed.
// None of it touches the save: it reads the unlock state and writes nothing, so no tool scene can
// mark anything found through it.
public partial class StageHintDirector : Node
{
	public Node2DGame Game { get; set; }
	public SideEvent Event { get; set; }
	public StageHint Hint { get; set; }

	// How far toward the source the cue is played from the player. Inside SfxPlayer's 900 px
	// audible radius, so it is heard (about 3.5 dB down), and far enough to the side that the 2D
	// listener pans it toward where it came from.
	private const float SoundOffset = 300f;

	private StageHintCaption caption;
	private int shown;
	private bool retired;

	/// <summary>How many hints this run has played.</summary>
	public int Shown => shown;

	public override void _Ready()
	{
		var overlay = Game?.GetNodeOrNull<CanvasLayer>("UIOverlay");
		if (overlay != null)
		{
			caption = new StageHintCaption { Name = "StageHintCaption" };
			overlay.AddChild(caption);
		}
	}

	public override void _ExitTree()
	{
		if (caption != null && IsInstanceValid(caption))
			caption.QueueFree();
	}

	public override void _Process(double delta)
	{
		if (retired || Game == null || Hint == null)
			return;

		if (Game.RunFinished || Game.BossFightActive || !StillHidden())
		{
			Retire();
			return;
		}

		float due = StageHintCatalog.DuePaceSeconds(shown);
		if (due > StageHintCatalog.LastPaceSeconds)
		{
			retired = true;
			return;
		}
		if (Game.PaceSeconds >= due)
			Play();
	}

	/// <summary>
	/// Plays the next hint now, whatever the pace clock says - for _UiShot --force-hint. Every other
	/// rule still holds: no hint for a found place or an owned discovery. Returns whether it played.
	/// </summary>
	public bool ForceHint()
	{
		if (retired || Game == null || Hint == null || !StillHidden())
			return false;
		return Play();
	}

	// The event is still out there, unfound and unmarked. Read every frame: four sites at most.
	private bool StillHidden()
	{
		if (Event == null || !IsInstanceValid(Event) || !Event.IsDormant)
			return false;
		foreach (PointOfInterest site in Event.Sites)
		{
			if (site.Marked)
				return false;
		}
		return true;
	}

	private void Retire()
	{
		retired = true;
		caption?.Dismiss();
	}

	private bool Play()
	{
		Player player = Game.RunPlayer;
		if (player == null || player.IsDead)
			return false;

		// The save, checked at the moment of playing rather than trusted from placement: once a
		// discovery is owned there is nothing out there to call for.
		var saveManager = GetNodeOrNull<SaveManager>("/root/SaveManager");
		if (Event.PaysChest || (saveManager?.Data != null && DiscoveryRewards.IsSiteExhausted(saveManager.Data, Hint.SiteId)))
		{
			retired = true;
			return false;
		}

		Vector2 source = NearestSite(player.GlobalPosition);
		string line = StageHintCatalog.LineFor(Hint, shown);
		shown++;

		caption?.ShowLine(line, Hint.Voice, player, source, Event.FarTellColor);
		Vector2 toward = source - player.GlobalPosition;
		Vector2 heard = player.GlobalPosition + (toward.LengthSquared() > 1f ? toward.Normalized() : Vector2.Up) * SoundOffset;
		SfxPlayer.AtPosition(Hint.Sound, heard, 0f);
		GD.Print($"StageHint: {Hint.StageId} hint {shown} at pace {Game.PaceSeconds:0}s, "
			+ $"{toward.Length():0} px away - \"{line}\"");
		return true;
	}

	// The nearest part of the event that is not done: the cell, the grove, the closest lantern.
	private Vector2 NearestSite(Vector2 from)
	{
		Vector2 best = Vector2.Zero;
		float bestDistance = float.MaxValue;
		foreach (PointOfInterest site in Event.Sites)
		{
			if (site.Done)
				continue;
			float d = from.DistanceSquaredTo(site.Position);
			if (d < bestDistance)
			{
				bestDistance = d;
				best = site.Position;
			}
		}
		return bestDistance < float.MaxValue ? best : from + Vector2.Up;
	}
}
