using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using WizardSurvivors.scripts;

// THE VAULT WALKS: --secret-probe on a chapter whose spell vault is not a breakable (the Geode Heart's is
// VaultProbe, in _UiShot.cs). One walk for every vault, and the part each one owes on its own:
//
//   the walk (VaultWalk) - placed five to seven chunks out on a fixed seed, never a target, solid or not as
//   the vault says, not seen before he has been near it and seen at it (with the mark on the save), the
//   far tell thicker within a chunk than beyond and its near pieces only near; then, once the vault's own
//   work has opened it, the pay: found, its deed recorded, the spell owned, in the level-up pool and NOT in
//   the loadout - and on a save that had already found it (--all-unlocked), a chest from the budget and
//   nothing granted.
//
//   the work (a subclass's Work) - the verb, driven the way a player would: the Swallow-Hole's mud and its
//   lure, the Broken Ward's stones, the Buried Runes' stillness, the Fulgurite's storm.
//
// PASS / FAIL lines, exit code = failures, <vault id>-N.png at each beat (under xvfb). The run's own swarm is
// cleared every frame but for the bodies a probe sets down itself. Writes are off: the save is in memory.
public partial class _UiShot
{
	/// <summary>The probe that works this chapter's vault: a walk of its own, or the Geode Heart's strike walk.</summary>
	private static Node NewVaultProbe(SecretDirector director, Player hero) => director.Vault switch
	{
		SwallowHole => new SwallowHoleWalk { Director = director, Hero = hero },
		_ => new VaultProbe { Director = director, Hero = hero },
	};

	private abstract partial class VaultWalk : Node
	{
		public SecretDirector Director;
		public Player Hero;
		protected SecretVault Vault;
		protected SaveData Save;
		protected bool HadBefore;
		protected string SpellId = "";
		private int failures;
		private int shots;
		private float wait;
		private int step;
		private Vector2 stand;
		private int chestsBefore;
		private float clock;
		private float workClock;

		/// <summary>The bodies the probe set down itself: kept when the rest of the swarm is cleared.</summary>
		protected readonly HashSet<Enemy> Kept = new();
		/// <summary>Where he stands, pinned there every frame while true.</summary>
		protected Vector2? Pin;
		/// <summary>His health held full every frame while true.</summary>
		protected bool Immortal = true;
		/// <summary>Movement keys held by the work; released every frame otherwise.</summary>
		protected bool Steering;

		/// <summary>Where to stand on arrival, from the vault's position.</summary>
		protected virtual Vector2 Arrive => new Vector2(0f, 230f);
		/// <summary>True if the vault's foot is solid in the navigation grid; false if it must not be.</summary>
		protected abstract bool ExpectSolid { get; }
		/// <summary>The vault's own work, a frame at a time: true once it has been opened (or the walk failed).</summary>
		protected abstract bool Work(float delta, float seconds);
		/// <summary>The longest the work may take, in seconds of game time.</summary>
		protected virtual float WorkLimit => 60f;

		protected void Check(string what, bool ok)
		{
			if (!ok)
				failures++;
			GD.Print($"VAULTWALK {(ok ? "PASS" : "FAIL")} {what}");
		}

		protected void Shot(string what)
		{
			shots++;
			if (DisplayServer.GetName() == "headless")
				return;
			Image image = GetViewport().GetTexture().GetImage();
			string path = OS.GetUserDataDir() + $"/{Vault?.SecretId ?? "vault"}-{shots}.png";
			image.SavePng(path);
			GD.Print($"VAULTWALK shot {path} - {what}");
		}

		protected void Wait(float seconds) => wait = seconds;

		protected void Finish()
		{
			GD.Print($"VAULTWALK {(failures == 0 ? "ALL PASS" : $"{failures} FAILED")}");
			GetTree().Quit(failures);
			step = -1;
		}

		protected bool InPool() => Hero.GetDealableCandidates()
			.Any(o => o.SpellId.Equals(SpellId, StringComparison.OrdinalIgnoreCase));

		protected Enemy Body(Vector2 at, bool elite = false, float health = 4000f)
		{
			Enemy e = Director.Game.SpawnEventEnemy(BestiaryRole.Bulk, at, health, elite);
			if (e != null)
			{
				e.GlobalPosition = at;
				Kept.Add(e);
			}
			return e;
		}

		/// <summary>Puts his FEET at <paramref name="foot"/> (the mud and the hazards measure the boots).</summary>
		protected void StandFeetAt(Vector2 foot)
		{
			Hero.GlobalPosition = foot - (Hero.FootPosition - Hero.GlobalPosition);
		}

		private void Hold()
		{
			if (!Steering)
				foreach (string action in new[] { "move_left", "move_right", "move_up", "move_down" })
					Input.ActionRelease(action);
			foreach (Enemy e in EnemyRegistry.Snapshot)
				if (e != null && IsInstanceValid(e) && !Kept.Contains(e))
					e.QueueFree();
			Kept.RemoveWhere(e => e == null || !IsInstanceValid(e));
			if (Immortal)
			{
				Hero.MaxHP = Math.Max(Hero.MaxHP, 20);
				Hero.CurrentHP = Hero.MaxHP;
			}
			if (Pin is Vector2 at)
				Hero.GlobalPosition = at;
		}

		public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

		// A level-up from a kill pauses the tree; the tool stops dismissing them once its shutter has fired,
		// and a walk outlasts the shutter, so the walk takes the first card itself and does nothing else.
		private static bool DismissLevelUp(Node root)
		{
			if (root is LevelUpMenu menu && menu.Visible)
			{
				IReadOnlyList<LevelUpOption> options = menu.GetOfferedOptions();
				if (options != null && options.Count > 0)
					menu.EmitSignal(LevelUpMenu.SignalName.WeaponSelected, options[0].AutoChoice());
				return true;
			}
			foreach (Node child in root.GetChildren())
				if (DismissLevelUp(child))
					return true;
			return false;
		}

		public override void _Process(double delta)
		{
			if (step < 0)
				return;
			if (GetTree().Paused)
			{
				DismissLevelUp(GetTree().Root);
				return;
			}
			float dt = (float)delta;
			clock += dt;
			Hold();
			if (wait > 0f)
			{
				wait -= dt;
				return;
			}
			switch (step)
			{
				case 0:
				{
					Vault = Director.Vault as SecretVault;
					Save = Director.GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data;
					Check($"the chapter's vault was placed ({Director.Vault?.SecretId})", Vault != null && Save != null);
					if (Vault == null || Save == null)
					{
						Finish();
						return;
					}
					SpellId = SecretCatalog.Get(Vault.SecretId)?.SpellUnlockId ?? "";
					HadBefore = Save.HasFoundSecret(Vault.SecretId);
					float chunks = Vault.GlobalPosition.DistanceTo(Director.Game.StageOrigin) / 1536f;
					GD.Print($"VAULTWALK {Vault.SecretId} at {Vault.GlobalPosition}, {chunks:0.0} chunks out; pays {SpellId}; save had it found before: {HadBefore}");
					Check($"it is five to seven chunks out ({chunks:0.00})", chunks >= SecretDirector.VaultMinChunks - 0.3f && chunks <= SecretDirector.VaultMaxChunks + 1.1f);
					Check("it is never a target: not in \"enemies\", nor anything in it",
						!Vault.IsInGroup("enemies") && !Vault.FindChildren("*", "", true, false).Any(n => n.IsInGroup("enemies")));
					bool solid = Director.Game.Terrain?.IsFenceAtWorld(Vault.GlobalPosition) == true;
					Check(ExpectSolid ? "its foot is solid in the navigation grid" : "it is not solid ground: the horde can walk in", solid == ExpectSolid);
					Check("not seen before he has been near it", !Director.IsGlimpsed(Vault.SecretId));
					if (!HadBefore)
						Check($"{SpellId} is not in the level-up pool before it is found", !InPool());
					stand = Vault.GlobalPosition + Arrive;
					step = 1;
					return;
				}
				case 1:
				{
					// A third of a chunk a step, every few frames, so the ground streams in round him.
					Vector2 to = stand - Hero.GlobalPosition;
					float left = to.Length();
					float toTell = Hero.GlobalPosition.DistanceTo(Vault.TellPosition);
					if (toTell > SecretDirector.SeenRadius + 50f && toTell <= SecretDirector.SeenRadius + 562f)
						Check($"not seen at {toTell:0} px from its tell", !Director.IsGlimpsed(Vault.SecretId));
					Hero.GlobalPosition += left <= 512f ? to : to / left * 512f;
					Wait(0.1f);
					if (left <= 512f)
					{
						step = 2;
						Wait(1.5f);
					}
					return;
				}
				case 2:
				{
					Check($"seen at it ({Hero.GlobalPosition.DistanceTo(Vault.TellPosition):0} px from its tell)", Director.IsGlimpsed(Vault.SecretId));
					Check("and the seen mark is on the save", Save.HasGlimpsedSecret(Vault.SecretId));
					int inner = 0, outer = 0, near = 0, nearFar = 0;
					foreach (Node n in GetTree().GetNodesInGroup(SecretFarTell.Group))
					{
						if (n is not Node2D s) continue;
						float d = s.GlobalPosition.DistanceTo(Vault.GlobalPosition);
						if (d <= 1536f) inner++;
						else if (d <= 3072f) outer++;
					}
					foreach (Node n in GetTree().GetNodesInGroup(SecretFarTell.GroupNear))
					{
						if (n is not Node2D s) continue;
						if (s.GlobalPosition.DistanceTo(Vault.GlobalPosition) <= 1536f) near++;
						else nearFar++;
					}
					GD.Print($"VAULTWALK far tell: {inner} within a chunk (+{near} near pieces), {outer} from one to two chunks out (+{nearFar} near pieces)");
					// Per area: the inner disc (less the footprint) is about a third of the outer annulus's.
					Check("the far tell thickens toward it (density within a chunk > from one to two)",
						inner + near > 0 && (inner + near) / 1f > (outer + nearFar) / 3f);
					Check("its near pieces are only near", near > 0 && nearFar == 0);
					Shot("arrived");
					chestsBefore = Director.Game.GetChildren().Count(c => c is ChestReward);
					workClock = 0f;
					step = 3;
					return;
				}
				case 3:
				{
					workClock += dt;
					if (Work(dt, workClock))
					{
						step = 4;
						return;
					}
					if (workClock > WorkLimit)
					{
						Check($"the work opened it within {WorkLimit:0} s", false);
						Finish();
					}
					return;
				}
				case 4:
				{
					// Wait for the light to reach him and the vault to pay.
					workClock += dt;
					if (!Vault.IsOpened && workClock < WorkLimit + 10f)
						return;
					// What the work set down has done its job; and the toast fades in over a beat.
					foreach (Enemy e in Kept)
						if (e != null && IsInstanceValid(e))
							e.QueueFree();
					Kept.Clear();
					step = 5;
					Wait(0.7f);
					return;
				}
				case 5:
				{
					Shot("the light reached him: the toast");
					Check("the vault is opened for the run", Vault.IsOpened);
					Check("it is found on the save", Save.HasFoundSecret(Vault.SecretId));
					Check("its deed is recorded", GlobalStatsManager.IsAchievementUnlocked(Save, AchievementDefinitions.SecretDeedId(Vault.SecretId)));
					Check($"{SpellId} is owned on the save", GlobalStatsManager.IsSpellUnlockedForLevelUp(Save, SpellId));
					Check($"{SpellId} joins the level-up pool in this run", InPool());
					Check("and is not put into the run (not in the loadout)",
						!Hero.GetEquippedSpells().Any(s => s != null && s.Id.Equals(SpellId, StringComparison.OrdinalIgnoreCase)));
					int chests = Director.Game.GetChildren().Count(c => c is ChestReward);
					if (HadBefore)
						Check($"found before: a chest from the budget instead ({chestsBefore} -> {chests}, paid {Director.ChestsPaid})", chests == chestsBefore + 1 && Director.ChestsPaid == 1);
					else
						Check($"found for the first time: no chest, the spell is the reward ({chestsBefore} -> {chests})", chests == chestsBefore && Director.ChestsPaid == 0);
					step = 6;
					Wait(0.5f);
					return;
				}
				case 6:
					Finish();
					return;
			}
		}
	}

	// THE SWALLOW-HOLE (8.6): its mud is a hazard to him - wading in slows him to a crawl and stings - and
	// not solid; an elite walked into it is NOT taken; a horde chasing him across it is, thirty of them, with
	// no XP and no kill; full, it heaves, and the light comes to him.
	private partial class SwallowHoleWalk : VaultWalk
	{
		private SwallowHole hole;
		private int sub;
		private float mark;
		private Vector2 from;
		private float freeStep;
		private int hpBefore;
		private Enemy elite;
		private int waves;
		private int orbsBefore;
		private int kills;
		private bool heaveShot;

		protected override bool ExpectSolid => false;
		protected override float WorkLimit => 90f;

		private static int Orbs(SceneTree tree) => tree.GetNodesInGroup(XPOrb.GroupName).Count;

		private void OnKilled(Enemy enemy, Vector2 at, SpellData source)
		{
			if (Kept.Contains(enemy))
				kills++;
		}

		public override void _ExitTree() => Player.EnemyKilled -= OnKilled;

		protected override bool Work(float delta, float seconds)
		{
			hole ??= Vault as SwallowHole;
			if (hole == null)
			{
				Check("the vault is a SwallowHole", false);
				Finish();
				return false;
			}
			Vector2 c = hole.GlobalPosition;
			switch (sub)
			{
				case 0:
					// A free step first, on the apron outside the mud, to measure him against.
					Pin = null;
					StandFeetAt(c + new Vector2(-40f, SwallowHole.HoleRadius + 60f));
					Steering = true;
					Input.ActionPress("move_right");
					from = Hero.GlobalPosition;
					mark = seconds;
					sub = 1;
					return false;
				case 1:
					if (seconds - mark < 0.5f)
						return false;
					freeStep = Hero.GlobalPosition.DistanceTo(from) / (seconds - mark);
					Input.ActionRelease("move_right");
					// Then the same step in the mud, with his health let go.
					StandFeetAt(c + new Vector2(-46f, 10f));
					Immortal = false;
					hpBefore = Hero.CurrentHP;
					Input.ActionPress("move_right");
					from = Hero.GlobalPosition;
					mark = seconds;
					sub = 2;
					return false;
				case 2:
					if (seconds - mark < 0.6f)
						return false;
					float wadeStep = Hero.GlobalPosition.DistanceTo(from) / (seconds - mark);
					GD.Print($"VAULTWALK walking: {freeStep:0} px/s on the bank, {wadeStep:0} px/s in the mud");
					Check($"wading slows him to a crawl ({wadeStep:0} px/s against {freeStep:0})", freeStep > 50f && wadeStep < freeStep * 0.45f);
					Check("he is wading", hole.IsWading);
					Input.ActionRelease("move_right");
					Steering = false;
					Pin = Hero.GlobalPosition;
					mark = seconds;
					sub = 3;
					return false;
				case 3:
					if (seconds - mark < 2.2f)
						return false;
					Check($"the mud stings: {hpBefore} -> {Hero.CurrentHP} HP in 2.8 s", Hero.CurrentHP < hpBefore);
					Shot("wading in the mud");
					Immortal = true;
					// An elite walked into the mud is not the hole's to take.
					Pin = c + new Vector2(0f, SwallowHole.HoleRadius + 120f);
					elite = Body(c + new Vector2(10f, -10f), elite: true);
					mark = seconds;
					sub = 4;
					return false;
				case 4:
					if (seconds - mark < 0.6f)
						return false;
					Check($"an elite in the mud is not taken ({hole.Taken} taken)", elite != null && IsInstanceValid(elite) && elite.IsTargetable && hole.Taken == 0);
					if (elite != null && IsInstanceValid(elite))
					{
						Kept.Remove(elite);
						elite.QueueFree();
					}
					// The lure: he stands on the near rim, and bodies come at him from beyond the hole.
					Pin = c + new Vector2(0f, SwallowHole.HoleRadius + 40f);
					Player.EnemyKilled += OnKilled;
					orbsBefore = Orbs(GetTree());
					mark = seconds;
					sub = 5;
					return false;
				case 5:
				{
					// Three waves of twelve, a beat apart, from the far side: they chase him straight across.
					if (waves < 3 && seconds - mark >= waves * 2.5f)
					{
						for (int k = 0; k < 12; k++)
							Body(c + new Vector2(-150f + k * 27f, -330f - (k % 3) * 30f));
						waves++;
					}
					if (hole.Taken > 0 && hole.Taken < SwallowHole.NeededTaken && !heaveShot && hole.Taken >= 10)
					{
						heaveShot = true;
						Shot($"swallowing ({hole.Taken} taken)");
					}
					if (hole.IsFull)
					{
						GD.Print($"VAULTWALK full after {seconds - mark:0.0} s of luring");
						Check($"thirty taken ({hole.Taken})", hole.Taken == SwallowHole.NeededTaken);
						Check($"none of them was killed ({kills} kills)", kills == 0);
						Check($"no XP left by what it took ({orbsBefore} -> {Orbs(GetTree())} orbs)", Orbs(GetTree()) <= orbsBefore);
						mark = seconds;
						sub = 6;
						return false;
					}
					if (seconds - mark > 40f)
					{
						Check($"the horde filled it within 40 s ({hole.Taken} of {SwallowHole.NeededTaken})", false);
						Finish();
					}
					return false;
				}
				case 6:
					if (seconds - mark < 0.55f)
						return false;
					Shot("full: it heaves");
					return true;
			}
			return false;
		}
	}
}
