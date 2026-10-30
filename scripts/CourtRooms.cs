using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WizardSurvivors.scripts;

// THE COURT'S ROOMS (.ai/endgame-castle.md 5.4): one small behaviour per room kind, attached by CourtRules.
//
// A room is a plain object that lives for the whole run, NOT a node: its furniture is built when the
// player comes within reach of it and freed when he is far away (CourtRules, as the room's ground
// streams in and out), and everything the room remembers - which fowl are eaten, whether its chest has
// been given, when the Chapel's ward re-arms - is kept here, on the object, so a room built again is the
// room he left. It is also mirrored into the secret run state (SecretDirector.StateOf, keyed
// "court_room:<id>") the moment the room is found, which is where the castle's secrets and achievements
// (piece C9) read it.
//
// What each room DOES in a run is here; what it pays ONCE EVER (Arcane Energy, a page mark, the Library's
// reveal of every secret's hint, "Every Door") is the achievements' and is not paid by anything in this
// file. The hooks those pieces attach to are on CourtRules: RoomFound and RoomEntered, IsRoomFound,
// KeepsakeDoorsOpen, and the layout itself.
//
// No collision on furniture. A banquet table, a pew or a bookcase with a body would be an obstacle the
// flow field does not know about (props are never in the navigation grid, .ai/terrain.md section 9), and
// a swarm wedged on a table's corner is the one thing the halls must never do; low furniture lies on the
// floor (drawn under the bodies) and tall furniture stands against the walls. The one exception is a
// shut Keepsake Door, whose body is in an alcove's mouth that nothing can need to walk through.
public static class CourtRooms
{
	/// <summary>
	/// Which chapter's keepsake each of the Keepsake Doors' ten alcoves holds, west to east (5.4: "one for
	/// each campaign chapter's keepsake") - the ten chapters of the road as it stood when the Court was
	/// designed, in that road's order. An explicit list, so a later move of a chapter (the Cursed Dungeon,
	/// .ai/endgame-castle.md 1.3) changes no door.
	/// </summary>
	public static readonly IReadOnlyList<string> KeepsakeDoorChapters = new[]
	{
		"stage_0", "stage_9", "stage_10", "stage_2", "stage_1", "stage_3", "stage_4", "stage_5", "stage_6", "stage_7",
	};

	/// <summary>The keepsake secret a chapter's door opens on, from SecretCatalog (planned or built), or null.</summary>
	public static SecretDefinition KeepsakeFor(string stageId) =>
		SecretCatalog.Defined.FirstOrDefault(s => s.IsKeepsake && s.StageId.Equals(stageId, StringComparison.OrdinalIgnoreCase));

	/// <summary>The behaviour for a room of the layout.</summary>
	public static CourtRoom Create(CourtRules rules, CourtLayout.Room place) => place.Kind switch
	{
		"great_hall" => new GreatHallRoom { Rules = rules, Place = place },
		"kitchens" => new KitchensRoom { Rules = rules, Place = place },
		"chapel" => new ChapelRoom { Rules = rules, Place = place },
		"library" => new CourtRoom { Rules = rules, Place = place },
		"armoury" => new ArmouryRoom { Rules = rules, Place = place },
		"kennels" => new KennelsRoom { Rules = rules, Place = place },
		"map_room" => new MapRoom { Rules = rules, Place = place },
		"treasury" => new TreasuryRoom { Rules = rules, Place = place },
		"gallery" => new GalleryRoom { Rules = rules, Place = place },
		"minstrels" => new ChestRoom { Rules = rules, Place = place },
		"solar" => new ChestRoom { Rules = rules, Place = place },
		"keepsake" => new KeepsakeDoorsRoom { Rules = rules, Place = place },
		"throne_doors" => new ThroneDoorsRoom { Rules = rules, Place = place },
		_ => new CourtRoom { Rules = rules, Place = place },
	};
}

/// <summary>
/// One room of the Court: its place, its furniture (built near, freed far) and what it does. The plain
/// room - the Library, the Well Court, the stair - only stands there; the kinds below do more.
/// </summary>
public class CourtRoom
{
	public CourtRules Rules { get; init; }
	public CourtLayout.Room Place { get; init; }

	/// <summary>The room's nodes while it is built, under CourtRules' y-sorted holder; null when it is not.</summary>
	protected Node2D Root { get; private set; }

	public bool IsBuilt => Root != null && GodotObject.IsInstanceValid(Root);

	/// <summary>True once the player has stood in it this run.</summary>
	public bool Found { get; set; }

	protected Node2DGame Game => Rules.Game;
	protected Player Player => Rules.Game?.RunPlayer;

	/// <summary>The room's furniture from the layout, in layout order.</summary>
	protected IEnumerable<AuthoredLayout.Piece> Pieces =>
		Rules.Layout.Grid.Furniture.Where(p => p.Room == Place.Id);

	public void Build(Node2D holder)
	{
		if (IsBuilt)
			return;
		Root = new Node2D { Name = "Room_" + Place.Id, YSortEnabled = true };
		holder.AddChild(Root);
		foreach (AuthoredLayout.Piece piece in Pieces)
			BuildPiece(piece);
		OnBuilt();
	}

	public void Teardown()
	{
		if (!IsBuilt)
			return;
		OnTeardown();
		Root.QueueFree();
		Root = null;
	}

	/// <summary>A piece of furniture: its sprite, unless the room handles that kind itself.</summary>
	protected virtual void BuildPiece(AuthoredLayout.Piece piece) => Rules.PlaceSprite(Root, piece);

	/// <summary>After the furniture: whatever the room adds of its own (pickups, vents, guards).</summary>
	protected virtual void OnBuilt() { }

	/// <summary>Before its nodes go: let go of any reference into them.</summary>
	protected virtual void OnTeardown() { }

	/// <summary>Every frame of the run, built or not. Cheap: most rooms do nothing here.</summary>
	public virtual void Tick(float delta, bool playerInside) { }

	/// <summary>The player walked in. <paramref name="firstTime"/>: the first time this run.</summary>
	public virtual void OnEntered(bool firstTime) { }

	protected Vector2 PieceWorld(AuthoredLayout.Piece piece) => Rules.Layout.CellToWorld(piece.At);

	protected AuthoredLayout.Piece FirstPiece(string kind) => Pieces.FirstOrDefault(p => p.Kind == kind);

	/// <summary>A chest at a point of the room, once a run: the rooms' rewards are the run's ordinary chests.</summary>
	protected void GiveChest(Vector2 at) => Game?.SpawnRewardChestAt(Game.ReachablePoint(at), 0.6f);
}

/// <summary>
/// THE GREAT HALL: Eric's dining room. Three long tables laid for a feast for a court that is not coming,
/// and on their platters twelve roast fowl, each a pickup that heals like a flask (10 HP). One returns to
/// an empty platter every 60 s, so the Hall is somewhere to come back to. The horde walks through and
/// touches nothing: a fowl answers the player alone.
/// </summary>
public sealed class GreatHallRoom : CourtRoom
{
	/// <summary>What a fowl heals: a flask's worth (HealthPickup.HealAmount).</summary>
	public const int FowlHeal = 10;

	/// <summary>Seconds between one fowl returning and the next, while any platter is empty.</summary>
	public const float FowlReturnSeconds = 60f;

	/// <summary>The radius a fowl is taken from, round its platter: a body's width.</summary>
	public const float FowlReach = 22f;

	private const int Platters = 12;

	// Which platters hold a bird, by platter index (the layout's), kept for the run.
	private readonly bool[] full = Enumerable.Repeat(true, Platters).ToArray();
	private float returnTimer;

	// While built: each platter's nodes.
	private readonly Area2D[] fowlAreas = new Area2D[Platters];
	private readonly Sprite2D[] bones = new Sprite2D[Platters];
	private readonly Vector2[] platterAt = new Vector2[Platters];

	/// <summary>How many of the twelve hold a bird now.</summary>
	public int FowlOnTheTables => full.Count(f => f);

	/// <summary>Fowl eaten this run, for the achievements (C9) and the probe.</summary>
	public int FowlEaten { get; private set; }

	protected override void BuildPiece(AuthoredLayout.Piece piece)
	{
		if (piece.Kind != "platter")
		{
			base.BuildPiece(piece);
			return;
		}
		int i = piece.Index;
		if (i < 0 || i >= Platters)
			return;
		platterAt[i] = PieceWorld(piece);
		Rules.PlaceSprite(Root, piece, "platter", CourtRules.OnTableZ);
		bones[i] = Rules.MakeSprite("fowl-bones", platterAt[i] + new Vector2(0f, -4f), CourtRules.OnPlatterZ);
		Root.AddChild(bones[i]);
		bones[i].Visible = !full[i];
		if (full[i])
			fowlAreas[i] = MakeFowl(i);
	}

	protected override void OnTeardown()
	{
		Array.Clear(fowlAreas);
		Array.Clear(bones);
	}

	private Area2D MakeFowl(int i)
	{
		// A pickup that answers the player's body only (layer 1, which the player's body is on and no
		// enemy is), as HealthPickup's does; it does not fly to him - it sits on its dish to be walked to.
		var area = new Area2D
		{
			Name = $"Fowl{i}",
			Position = platterAt[i],
			CollisionLayer = 0,
			CollisionMask = 1,
			Monitorable = false,
			ZIndex = CourtRules.OnPlatterZ,
			ZAsRelative = false,
		};
		area.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = FowlReach } });
		Sprite2D sprite = Rules.MakeSprite("fowl", new Vector2(0f, -8f), 0);
		sprite.ZAsRelative = true;
		area.AddChild(sprite);
		int index = i;
		area.BodyEntered += body => OnFowlTouched(index, body);
		Root.AddChild(area);
		return area;
	}

	private void OnFowlTouched(int i, Node body)
	{
		if (!full[i] || body == null || !body.IsInGroup("player") || !body.HasMethod("Heal"))
			return;
		full[i] = false;
		FowlEaten++;
		int before = body is Player hero ? hero.CurrentHP : 0;
		body.Call("Heal", FowlHeal);
		if (body is Player healed)
			Rules.LastFowlHeal = (before, healed.CurrentHP);
		SfxPlayer.AtPosition(SfxCatalog.PickupHealth, platterAt[i], 0.03f);
		if (body is Node2D player)
			PickupFlourish.Play(player, PickupFlourish.Kind.Mend);
		Area2D area = fowlAreas[i];
		fowlAreas[i] = null;
		// Freed at the end of the frame: this is the area's own signal.
		if (area != null && GodotObject.IsInstanceValid(area))
			area.QueueFree();
		if (bones[i] != null && GodotObject.IsInstanceValid(bones[i]))
			bones[i].Visible = true;
	}

	public override void Tick(float delta, bool playerInside)
	{
		if (FowlOnTheTables >= Platters)
		{
			returnTimer = 0f;
			return;
		}
		returnTimer += delta;
		if (returnTimer < FowlReturnSeconds)
			return;
		returnTimer = 0f;
		// The empty platter nearest the hall's head (the lowest index) is laid again.
		for (int i = 0; i < Platters; i++)
		{
			if (full[i])
				continue;
			full[i] = true;
			if (IsBuilt)
			{
				if (bones[i] != null && GodotObject.IsInstanceValid(bones[i]))
					bones[i].Visible = false;
				fowlAreas[i] = MakeFowl(i);
			}
			break;
		}
	}

	/// <summary>For the probe: eat the fowl on platter <paramref name="i"/> as if the player had walked onto it.</summary>
	public bool DebugEat(int i, Node player)
	{
		if (i < 0 || i >= Platters || !full[i])
			return false;
		OnFowlTouched(i, player);
		return true;
	}

	/// <summary>Where platter <paramref name="i"/> stands, in world pixels (known once the hall has been built).</summary>
	public Vector2 PlatterPosition(int i) => platterAt[Math.Clamp(i, 0, Platters - 1)];

	public bool PlatterFull(int i) => i >= 0 && i < Platters && full[i];
}

/// <summary>
/// THE KITCHENS: the ovens the fowl came from, each with a flame vent in front of it (the chapter's one
/// trap, 5.9), and a cook's larder with two flasks in it, once a run.
/// </summary>
public sealed class KitchensRoom : CourtRoom
{
	private readonly bool[] flaskTaken = new bool[2];
	private readonly HealthPickup[] flasks = new HealthPickup[2];
	private bool tearingDown;
	private int flaskIndex;
	private int ventIndex;

	protected override void BuildPiece(AuthoredLayout.Piece piece)
	{
		if (piece.Kind == "vent")
		{
			var scene = ResourceLoader.Load<PackedScene>("res://scenes/FlameVent.tscn");
			if (scene?.Instantiate() is Node2D vent)
			{
				vent.Position = PieceWorld(piece);
				// Desynchronised, as the stage's own hazards are (Node2DGame.BuildStageHazards): three
				// ovens breathing on one beat would be one wall of fire to time.
				if (vent is StageHazard hazard)
					hazard.StartPhaseOffset = ventIndex * 1.3f;
				ventIndex++;
				Root.AddChild(vent);
			}
			return;
		}
		if (piece.Kind == "flask_spot")
		{
			int k = flaskIndex++;
			if (k >= flasks.Length || flaskTaken[k])
				return;
			var scene = ResourceLoader.Load<PackedScene>("res://scenes/HealthPickup.tscn");
			if (scene?.Instantiate() is HealthPickup flask)
			{
				flask.Position = PieceWorld(piece);
				// Taken (it frees itself on pickup) unless it is the room being torn down that frees it.
				flask.TreeExiting += () => { if (!tearingDown) flaskTaken[k] = true; };
				flasks[k] = flask;
				Root.AddChild(flask);
			}
			return;
		}
		base.BuildPiece(piece);
	}

	protected override void OnBuilt()
	{
		flaskIndex = 0;
		ventIndex = 0;
		tearingDown = false;
	}

	protected override void OnTeardown()
	{
		tearingDown = true;
		Array.Clear(flasks);
	}

	/// <summary>Flasks still in the larder this run.</summary>
	public int FlasksLeft => flaskTaken.Count(t => !t);
}

/// <summary>
/// THE CHAPEL: a ward. For 20 s after he enters, the horde will not cross the threshold - anything of it
/// inside is turned back out of the door, as the Broken Ward's repelling edge turns it - and then it rests
/// for 90 s before it will hold again. Bolts still come through the door: it holds bodies, not the air.
/// </summary>
public sealed class ChapelRoom : CourtRoom
{
	public const float WardSeconds = 20f;
	public const float RearmSeconds = 90f;
	private const float PushSpeed = 420f;

	private float wardLeft;
	private float rearmLeft;
	private Rect2 door;
	private Vector2 outside;
	private Polygon2D threshold;

	public bool WardActive => wardLeft > 0f;

	protected override void OnBuilt()
	{
		FindDoor();
		threshold = new Polygon2D
		{
			Name = "Ward",
			Polygon = new[] { door.Position, new Vector2(door.End.X, door.Position.Y), door.End, new Vector2(door.Position.X, door.End.Y) },
			// The player's own light (the Light element's mid), faint: a ward he carries in, not a wall.
			Color = new Color(1f, 0.94f, 0.66f, 0f),
			ZIndex = DrawLayers.GroundTell,
			ZAsRelative = false,
		};
		Root.AddChild(threshold);
	}

	protected override void OnTeardown() => threshold = null;

	private void FindDoor()
	{
		foreach (AuthoredLayout.Gap d in Rules.Layout.Grid.Doors)
		{
			if (d.Room != Place.Id)
				continue;
			door = Rules.Layout.CellsToWorld(d.Rect);
			Vector2 c = door.GetCenter();
			Vector2 away = (c - Place.Centre);
			away = Mathf.Abs(away.X) > Mathf.Abs(away.Y) ? new Vector2(Mathf.Sign(away.X), 0f) : new Vector2(0f, Mathf.Sign(away.Y));
			outside = c + away * 120f;
			return;
		}
	}

	public override void OnEntered(bool firstTime)
	{
		if (rearmLeft > 0f || wardLeft > 0f)
			return;
		if (door.Size == Vector2.Zero)
			FindDoor();
		wardLeft = WardSeconds;
		SfxPlayer.AtPosition(SfxCatalog.EventReward, Place.Centre, 0.02f);
	}

	public override void Tick(float delta, bool playerInside)
	{
		if (wardLeft > 0f)
		{
			wardLeft -= delta;
			if (wardLeft <= 0f)
				rearmLeft = RearmSeconds;
			Rect2 inside = Place.World.Grow(24f).Merge(door);
			foreach (Enemy enemy in EnemyRegistry.Snapshot)
			{
				if (enemy == null || !GodotObject.IsInstanceValid(enemy) || enemy is BossEnemy || enemy.IsDying)
					continue;
				Vector2 p = enemy.GlobalPosition;
				if (!inside.HasPoint(p))
					continue;
				enemy.ApplyKnockback((outside - p).Normalized() * PushSpeed);
			}
		}
		else if (rearmLeft > 0f)
		{
			rearmLeft -= delta;
		}
		if (threshold != null && GodotObject.IsInstanceValid(threshold))
		{
			float a = wardLeft > 0f ? 0.18f + 0.08f * Mathf.Sin(Rules.Elapsed * 5f) : 0f;
			threshold.Color = new Color(1f, 0.94f, 0.66f, a);
		}
	}
}

/// <summary>THE ARMOURY: racks of arms. Once a run, plate - a shield of a quarter of his health - and a chest.</summary>
public sealed class ArmouryRoom : CourtRoom
{
	private bool given;

	public override void OnEntered(bool firstTime)
	{
		if (given || Player == null)
			return;
		given = true;
		// .ai/endgame-castle.md 5.4 asks for 60 s of +15% Armour. Armour is Player's one mitigation number
		// and has no timed source; until Player grows one (a hook for the dark wizard's agent's file, not
		// this one's), the Armoury's plate is a shield: the same kind of thing, spent by damage instead of
		// by the clock.
		Player.AddShield(Mathf.RoundToInt(Player.MaxHP * 0.25f));
		SfxPlayer.Global(SfxCatalog.EventReward);
		GiveChest(Place.Centre + new Vector2(0f, 48f));
	}
}

/// <summary>THE KENNELS: empty kennels and a broken chain. With the Familiar's hound in the loadout, the hound finds a chest in the straw.</summary>
public sealed class KennelsRoom : CourtRoom
{
	private bool given;

	public override void OnEntered(bool firstTime)
	{
		if (given || Player == null)
			return;
		if (!Player.GetEquippedSpells().Any(s => s != null && s.Id == "familiar"))
			return;
		given = true;
		AuthoredLayout.Piece straw = FirstPiece("straw");
		GiveChest(straw != null ? PieceWorld(straw) : Place.Centre);
	}
}

/// <summary>THE MAP ROOM: a table map of the house. Standing in it gives the Court's map for the rest of the run (5.2).</summary>
public sealed class MapRoom : CourtRoom
{
	public override void OnEntered(bool firstTime) => Rules.ShowMap();
}

/// <summary>
/// THE TREASURY: the Court's keepsake, guarded by the Chamberlain (the chapter's miniboss, 5.7) - he comes
/// out when the player first steps in, and the chest is his to give up. The keepsake's once-ever reward
/// (150 Arcane Energy) is the castle's secrets' (C9).
/// </summary>
public sealed class TreasuryRoom : CourtRoom
{
	/// <summary>The Chamberlain's health, in ordinary spawns: the Gaoler's key-carrier's (GaolersKeyEvent).</summary>
	public const float GuardHealthMultiplier = 9f;

	private Enemy guard;
	private bool guardCame;
	private bool chestGiven;

	/// <summary>The Chamberlain while he stands; null before he comes and after he falls.</summary>
	public Enemy Guard => guard != null && GodotObject.IsInstanceValid(guard) && !guard.IsDying ? guard : null;

	public override void OnEntered(bool firstTime)
	{
		if (guardCame || Game == null || Game.BossFightActive)
			return;
		guardCame = true;
		AuthoredLayout.Piece box = FirstPiece("strongbox");
		Vector2 at = box != null ? PieceWorld(box) + new Vector2(0f, 96f) : Place.Centre;
		guard = Game.SpawnEventEnemy(BestiaryRole.MiniBoss, at, GuardHealthMultiplier, true);
		SfxPlayer.Global(SfxCatalog.EliteSpawn);
	}

	public override void Tick(float delta, bool playerInside)
	{
		if (!guardCame || chestGiven || Guard != null)
			return;
		chestGiven = true;
		AuthoredLayout.Piece box = FirstPiece("strongbox");
		GiveChest((box != null ? PieceWorld(box) : Place.Centre) + new Vector2(0f, 72f));
	}
}

/// <summary>
/// THE GALLERY: portraits of the wizards he took, their faces covered; those the player has freed are
/// uncovered. Standing among them heals by half, once a run.
/// </summary>
public sealed class GalleryRoom : CourtRoom
{
	private bool healed;

	/// <summary>The wizards he took, in catalog order: every wizard but the four disciplines the player starts as.</summary>
	public static IReadOnlyList<string> TakenWizards =>
		UnlockCatalog.AllWizardIds.Where(id => UnlockCatalog.GetCharacter(id)?.Source != UnlockSource.Starter).ToList();

	protected override void BuildPiece(AuthoredLayout.Piece piece)
	{
		if (piece.Kind != "portrait")
		{
			base.BuildPiece(piece);
			return;
		}
		IReadOnlyList<string> taken = TakenWizards;
		if (piece.Slot < 0 || piece.Slot >= taken.Count)
			return;            // a bare stretch of wall: there are more frames' places than wizards he took
		SaveData save = Rules.Save;
		bool freed = save != null && GlobalStatsManager.IsCharacterUnlocked(save, taken[piece.Slot]);
		Rules.PlaceSprite(Root, piece, freed ? "portrait" : "portrait-covered", CourtRules.WallDecorZ);
	}

	public override void OnEntered(bool firstTime)
	{
		if (healed || Player == null)
			return;
		healed = true;
		Player.Heal(Mathf.CeilToInt(Player.MaxHP * 0.5f));
		SfxPlayer.Global(SfxCatalog.PickupHealth);
		PickupFlourish.Play(Player, PickupFlourish.Kind.Mend);
	}
}

/// <summary>A room whose reward is one chest, once a run, on its chest spot: the Minstrels' Gallery and the Solar.</summary>
public sealed class ChestRoom : CourtRoom
{
	private bool given;

	public override void OnEntered(bool firstTime)
	{
		if (given)
			return;
		given = true;
		AuthoredLayout.Piece spot = FirstPiece("chest_spot");
		GiveChest(spot != null ? PieceWorld(spot) : Place.Centre);
	}
}

/// <summary>
/// THE KEEPSAKE DOORS: a corridor of ten locked doors, one for each chapter keepsake. Every keepsake the
/// save has found opens its door: behind it, that keepsake on a plinth in its alcove, and a heal. A shut
/// door is a body in the alcove's mouth (the terrain layer, which feet and enemies meet); an alcove behind
/// one is closed to placement (CourtRules.IsPlacementBlocked).
/// </summary>
public sealed class KeepsakeDoorsRoom : CourtRoom
{
	private readonly bool[] open = new bool[CourtRooms.KeepsakeDoorChapters.Count];
	private readonly bool[] healTaken = new bool[CourtRooms.KeepsakeDoorChapters.Count];
	private bool decided;
	private bool tearingDown;

	/// <summary>How many of the ten this save has opened ("Every Door" is all ten: the achievements', C9).</summary>
	public int OpenCount
	{
		get
		{
			Decide();
			return open.Count(o => o);
		}
	}

	public bool IsOpen(int index)
	{
		Decide();
		return index >= 0 && index < open.Length && open[index];
	}

	// Read once a run, from the save: a keepsake found mid-run in the Court itself would be another
	// chapter's, so nothing can change while the player stands here.
	private void Decide()
	{
		if (decided)
			return;
		decided = true;
		SaveData save = Rules.Save;
		for (int k = 0; k < open.Length; k++)
		{
			SecretDefinition keepsake = CourtRooms.KeepsakeFor(CourtRooms.KeepsakeDoorChapters[k]);
			open[k] = keepsake != null && save != null && save.HasFoundSecret(keepsake.Id);
		}
	}

	protected override void BuildPiece(AuthoredLayout.Piece piece)
	{
		Decide();
		int k = piece.Index;
		if (piece.Kind == "keepsake_door" && k >= 0 && k < open.Length)
		{
			Rules.PlaceSprite(Root, piece, open[k] ? "keepsake-door-open" : "keepsake-door", 0);
			if (!open[k] && k < Rules.Layout.Alcoves.Count)
				Root.AddChild(DoorBody(Rules.Layout.Alcoves[k].Mouth));
			return;
		}
		if (piece.Kind == "reliquary" && k >= 0 && k < open.Length)
		{
			if (!open[k])
				return;
			Rules.PlaceSprite(Root, piece, "reliquary", 0);
			if (!healTaken[k])
			{
				var scene = ResourceLoader.Load<PackedScene>("res://scenes/HealthPickup.tscn");
				if (scene?.Instantiate() is HealthPickup flask)
				{
					flask.Position = PieceWorld(piece) + new Vector2(0f, 60f);
					flask.MagnetAttracted = false;
					int index = k;
					flask.TreeExiting += () => { if (!tearingDown) healTaken[index] = true; };
					Root.AddChild(flask);
				}
			}
			return;
		}
		base.BuildPiece(piece);
	}

	protected override void OnBuilt() => tearingDown = false;

	protected override void OnTeardown() => tearingDown = true;

	private static StaticBody2D DoorBody(Rect2 mouth)
	{
		var body = new StaticBody2D
		{
			Name = "ShutDoor",
			Position = mouth.GetCenter(),
			CollisionLayer = 1u << (LevelTilePainter.TerrainCollisionLayer - 1),
			CollisionMask = 0,
		};
		body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = mouth.Size } });
		return body;
	}

	/// <summary>True for a point inside an alcove whose door is shut.</summary>
	public bool IsInShutAlcove(Vector2 world)
	{
		Decide();
		IReadOnlyList<CourtLayout.Alcove> alcoves = Rules.Layout.Alcoves;
		for (int k = 0; k < alcoves.Count && k < open.Length; k++)
		{
			if (!open[k] && (alcoves[k].World.HasPoint(world) || alcoves[k].Mouth.HasPoint(world)))
				return true;
		}
		return false;
	}
}

/// <summary>
/// THE THRONE ROOM DOORS, in the vestibule's north wall. Shut through the run; when the Seneschal falls
/// they stand open and the way home opens in front of them (CourtRules.VictoryPortalPoint, hook 5).
/// </summary>
public sealed class ThroneDoorsRoom : CourtRoom
{
	private Sprite2D doors;
	private bool shownOpen;

	protected override void BuildPiece(AuthoredLayout.Piece piece)
	{
		if (piece.Kind != "throne_doors")
		{
			base.BuildPiece(piece);
			return;
		}
		shownOpen = Game != null && Game.VictoryPortalOpen;
		doors = Rules.PlaceSprite(Root, piece, shownOpen ? "throne-doors-open" : "throne-doors", 0);
	}

	protected override void OnTeardown() => doors = null;

	public override void Tick(float delta, bool playerInside)
	{
		if (shownOpen || doors == null || !GodotObject.IsInstanceValid(doors) || Game == null || !Game.VictoryPortalOpen)
			return;
		shownOpen = true;
		doors.Texture = Rules.Texture("throne-doors-open");
	}
}
