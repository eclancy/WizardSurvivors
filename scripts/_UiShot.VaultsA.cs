using Godot;
using System.Linq;
using WizardSurvivors.scripts;

// The VaultProbe's branches for the four vaults built by the first vaults agent (2026-10-30): the Pale Hart
// (follow), the Pinfold (guarded), the Fallen Star (gather, then linger) and the Cold Forge (cull). The
// walk to the vault, the seen mark and the far tell are the probe's shared steps 0-2 (_UiShot.cs); each
// branch starts at step 100 with the player standing at the vault, works it the way a player would - by
// where he stands and what dies near him - checks each promise of its row in .ai/achievements-and-
// secrets.md section 8 on the way, and ends in the shared CheckPaid. Moving the player is moving `stand`,
// which Hold() pins him to every frame.
public partial class _UiShot
{
	private partial class VaultProbe
	{
		private int sub;
		private float branchClock;
		private Vector2 lastHartAt;
		private bool leashTested;
		private int hops;

		private bool HasVaultsABranch() => Director.Vault is PaleHart or Pinfold or FallenStar or ColdForge;

		// Bodies Hold() must not clear: a branch's own (the forge's kills, set down to die in its ring) and a
		// vault's (the Pinfold's guards, which it wakes itself the moment he comes near, on the walk in).
		private readonly System.Collections.Generic.List<Enemy> keepAlso = new();

		private System.Collections.Generic.IEnumerable<Enemy> KeptByVaultsA() =>
			vault is Pinfold pound ? keepAlso.Concat(pound.Guards) : keepAlso;

		// One frame of the vault's own branch; false if this vault has none here.
		private bool WorkVaultsA(float dt)
		{
			branchClock += dt;
			switch (vault)
			{
				case PaleHart hart:
					WorkPaleHart(hart, dt);
					return true;
				case Pinfold pound:
					WorkPinfold(pound);
					return true;
				case FallenStar star:
					WorkFallenStar(star);
					return true;
				case ColdForge forge:
					WorkColdForge(forge);
					return true;
				default:
					return false;
			}
		}

		// --- D5, the Cold Forge: cull ----------------------------------------------------------------------

		private int culled;
		private float cullStarted;

		// One of the chapter's bulk, set down at a point and killed there by the player's own hit
		// (Player.DealDamageToEnemy, so Player.EnemyKilled fires where it fell, as in play).
		private bool KillAt(Vector2 at)
		{
			Enemy e = Director.Game.SpawnEventEnemy(BestiaryRole.Bulk, at, 1f, false);
			if (e == null)
				return false;
			e.GlobalPosition = at;
			Hero.DealDamageToEnemy(e, System.Math.Max(1, e.Health) * 4, 0f, false, null);
			return e.Health <= 0 || e.IsDying;
		}

		private void WorkColdForge(ColdForge forge)
		{
			Vector2 c = forge.GlobalPosition;
			switch (step)
			{
				case 100:
				{
					chestsBefore = Director.Game.GetChildren().Count(ch => ch is ChestReward);
					// No wall anywhere on its ring or inside it: a corridor is 288 px across, the ring 360.
					bool open = true;
					for (int k = 0; k < 24; k++)
						foreach (float r in new[] { ColdForge.RingRadius, ColdForge.RingRadius * 0.5f })
							open &= Director.Game.Terrain?.IsWallAtWorld(c + Vector2.FromAngle(k * Mathf.Tau / 24f) * r) == false;
					Check("it stands in a hall room, not a corridor: no wall on or in its ring", forge.InHall && open);
					GD.Print($"VAULTPROBE the forge's ring is at {c}; the hearth at {c + ColdForge.ForgeFoot}");
					// A kill just outside the ring counts for nothing.
					bool killed = KillAt(c + new Vector2(ColdForge.RingRadius + 50f, 0f));
					Check($"a kill outside the ring lights nothing ({forge.Embers} embers)", killed && forge.Embers == 0);
					stand = c + new Vector2(0f, 110f);
					step++;
					wait = 0.3f;
					cullStarted = branchClock;
					return;
				}
				case 101:
				{
					// The swarm dying on the hearth: one a frame, all over the ring.
					float a = culled * 2.4f;
					float r = 30f + (culled * 37 % 130);
					if (KillAt(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r))
						culled++;
					if (forge.Embers == 20 && culled == 20)
						Shot("twenty killed on the hearth: twenty embers in the coals");
					if (forge.Roared)
					{
						Check($"forty kills on the hearth light it ({culled} killed, {forge.Embers} embers)", culled == ColdForge.KillsNeeded && forge.Embers == ColdForge.KillsNeeded);
						step++;
						wait = 0.35f;
						return;
					}
					if (branchClock - cullStarted > 30f)
					{
						Check($"forty kills on the hearth light it within 30 s ({culled} killed, {forge.Embers} embers)", false);
						Finish();
					}
					return;
				}
				case 102:
				{
					Shot("the forge roars, the light leaves the anvil");
					step = 6;
					wait = 1.8f;
					return;
				}
			}
		}

		// --- BC1, the Fallen Star: gather, then linger -------------------------------------------------------

		// Out of the trail's order, on purpose: "in any order" is the verb.
		private static readonly int[] GatherOrder = { 3, 0, 6, 1, 5, 2, 4 };
		private int gathered;
		private float lingeredBefore;

		private void WorkFallenStar(FallenStar star)
		{
			SecretGather gather = star.Gather;
			switch (step)
			{
				case 100:
				{
					chestsBefore = Director.Game.GetChildren().Count(c => c is ChestReward);
					Check($"seven shards lie along the trail ({gather?.Count})", gather != null && gather.Count == FallenStar.Shards);
					// Along one straight line: each within 160 px of the line from the trail's far end to the crater.
					Vector2 a = star.TrailStart, b = star.GlobalPosition;
					Vector2 n = (b - a).Normalized();
					float worst = gather.Points.Max(p => Mathf.Abs((p - a).Cross(n)));
					float length = a.DistanceTo(b);
					GD.Print($"VAULTPROBE the trail runs {length:0} px to the crater, {star.BurnCount} burn marks; the shards lie within {worst:0} px of its line");
					Check("the shards lie along one straight line", worst <= 160f);
					Check("the burn runs the trail's length (a mark every 42 px, most on open heath)", star.BurnCount >= (int)(length / FallenStar.BurnStep * 0.6f));
					Check("it starts two chunks out from the crater or more (its far tell)", length >= 1.5f * 1536f);
					// In the crater before the shards are gathered: nothing.
					stand = star.GlobalPosition;
					step++;
					wait = FallenStar.LingerSeconds + 0.6f;
					return;
				}
				case 101:
				{
					Check("lingering in the crater before the shards are gathered does nothing", !star.Risen && star.Lingered <= 0f);
					gathered = 0;
					step = 102;
					return;
				}
				case 102:
				{
					stand = gather.Points[GatherOrder[gathered]];
					step = 103;
					wait = 0.3f;
					return;
				}
				case 103:
				{
					int k = GatherOrder[gathered];
					Check($"walking over shard {k} takes it ({gather.Gathered} of {gather.Count})", gather.IsTaken(k) && gather.Gathered == gathered + 1);
					gathered++;
					if (gathered == 3)
						Shot("on the trail: three shards taken, the burn running on");
					step = gathered < GatherOrder.Length ? 102 : 104;
					return;
				}
				case 104:
				{
					Check("all seven gathered, out of order", gather.IsComplete);
					stand = star.GlobalPosition;
					step++;
					wait = 1.0f;
					return;
				}
				case 105:
				{
					lingeredBefore = star.Lingered;
					Check($"a second in the crater is not enough ({lingeredBefore:0.00} s of {FallenStar.LingerSeconds:0})", !star.Risen && lingeredBefore > 0.5f);
					Shot("lingering: the star's light rising in its stone");
					stand = star.GlobalPosition + new Vector2(0f, 320f);
					step++;
					wait = 0.5f;
					return;
				}
				case 106:
				{
					Check($"stepping out lets it sink back ({lingeredBefore:0.00} -> {star.Lingered:0.00} s)", star.Lingered < lingeredBefore && !star.Risen);
					stand = star.GlobalPosition;
					step++;
					wait = FallenStar.LingerSeconds + 0.3f;
					return;
				}
				case 107:
				{
					Check("two seconds in the crater with every shard: the light rises", star.Risen);
					step = 6;
					wait = 1.6f;
					return;
				}
			}
		}

		// --- FF1, the Pinfold: guarded --------------------------------------------------------------------

		private void WorkPinfold(Pinfold pound)
		{
			switch (step)
			{
				case 100:
				{
					chestsBefore = Director.Game.GetChildren().Count(c => c is ChestReward);
					var guards = pound.Guards;
					Check($"coming near woke its two guards ({guards.Count})", guards.Count == 2);
					Check("both are elites (never recycled to the spawn ring)", guards.All(g => IsInstanceValid(g) && g.IsMiniBoss));
					Check("and both are the field's own enemies, in \"enemies\"", guards.All(g => IsInstanceValid(g) && g.IsInGroup("enemies")));
					GD.Print($"VAULTPROBE the guards: {string.Join(", ", guards.Where(IsInstanceValid).Select(g => g.SceneFilePath.GetFile()))}");
					Check("the pound is not in \"enemies\" and the latch is not lit while they stand", !pound.IsInGroup("enemies") && !pound.GuardsDown);
					// At the latch while they stand: it does not lift.
					stand = pound.Latch + new Vector2(0f, 36f);
					step++;
					wait = 0.6f;
					return;
				}
				case 101:
				{
					Check("the latch does not lift while its guards stand", !pound.GateOpen);
					Shot("the guards woken, the hound behind the bars");
					// The fight, by the player's own hits (Player.DealDamageToEnemy, the path every spell takes).
					foreach (Enemy guard in pound.Guards)
						if (IsInstanceValid(guard))
							Hero.DealDamageToEnemy(guard, System.Math.Max(1, guard.Health) * 4, 0f, false, null);
					step++;
					wait = 0.3f;
					return;
				}
				case 102:
				{
					Check("both guards are down", pound.Guards.All(g => !IsInstanceValid(g) || g.IsDying || g.Health <= 0));
					// He is standing at the latch: with the guards down it lifts at once.
					Check("with them down, the latch lifts and the hound gets up", pound.GuardsDown && pound.GateOpen);
					step++;
					wait = 0.75f;
					return;
				}
				case 103:
				{
					Shot("the gate open, the hound running to him");
					step = 6;
					wait = 1.4f;
					return;
				}
			}
		}

		// --- F5, the Pale Hart: follow ----------------------------------------------------------------------

		private void WorkPaleHart(PaleHart vaultHart, float dt)
		{
			PaleHart.HartFigure hart = vaultHart.Hart;
			switch (step)
			{
				case 100:
				{
					chestsBefore = Director.Game.GetChildren().Count(c => c is ChestReward);
					Check("the hart is there, and never a target", hart != null && !hart.IsInGroup("enemies"));
					Check($"its path is laid: home and {PaleHart.Stands} stands ({vaultHart.Path.Count - 1})", vaultHart.Path.Count == PaleHart.Stands + 1);
					float shortest = float.MaxValue, longest = 0f;
					for (int i = 1; i < vaultHart.Path.Count; i++)
					{
						float d = vaultHart.Path[i].DistanceTo(vaultHart.Path[i - 1]);
						shortest = Mathf.Min(shortest, d);
						longest = Mathf.Max(longest, d);
					}
					GD.Print($"VAULTPROBE the hart's bounds run {shortest:0} to {longest:0} px; the seat is at {vaultHart.Seat}, "
						+ $"{vaultHart.Seat.DistanceTo(vaultHart.Path[0]):0} px from its home");
					Check("each bound is about 400 px (300 to 600)", shortest >= 300f && longest <= 600f);
					// The far tell points at the clearing: every print and bent frond faces the site.
					int facing = 0, total = 0;
					foreach (string group in new[] { SecretFarTell.Group, SecretFarTell.GroupNear })
						foreach (Node n in GetTree().GetNodesInGroup(group))
						{
							if (n is not Sprite2D s || s.GlobalPosition.DistanceTo(vault.GlobalPosition) > 3200f)
								continue;
							total++;
							if (s.FlipH == (vault.GlobalPosition.X < s.GlobalPosition.X))
								facing++;
						}
					Check($"the prints and the bent bracken all point at it ({facing} of {total})", total > 0 && facing == total);
					GD.Print($"VAULTPROBE the hart is at stand {hart?.State.Step} after the walk in");
					// Straight to the seat, before the hart has led him there: the bow is not his yet.
					stand = vaultHart.Seat + new Vector2(0f, 30f);
					step++;
					wait = 0.8f;
					return;
				}
				case 101:
				{
					Check("standing at the seat before the hart does takes nothing", !vaultHart.BowTaken && !vault.IsOpened);
					// Now follow it from wherever it is.
					branchClock = 0f;
					step++;
					return;
				}
				case 102:
				{
					// Follow: stand a little short of it, on the side away from where it goes next, so it
					// bounds on; wait for each bound to land.
					if (hart.HopT < 1f)
						return;
					int at = hart.State.Step;
					if (at != hops)
					{
						hops = at;
						GD.Print($"VAULTPROBE the hart bounded to stand {at} ({hart.GlobalPosition.DistanceTo(lastHartAt):0} px), {branchClock:0.0} s");
						if (at == 3 && !leashTested)
							Shot("following the hart: it waits, facing him");
					}
					lastHartAt = hart.GlobalPosition;
					if (hart.IsSettled)
					{
						Check($"it led him to the seat in {branchClock:0.0} s (the vault's ask: 20 s at most)", branchClock <= 20f);
						step++;
						wait = 0.4f;
						return;
					}
					if (at == 3 && !leashTested)
					{
						// Wander off part-way: it goes home.
						leashTested = true;
						Vector2 away = (hart.GlobalPosition - vaultHart.Seat).Normalized();
						stand = hart.GlobalPosition + (away.LengthSquared() > 0f ? away : Vector2.Down) * (PaleHart.LeashRadius + 200f);
						step = 110;
						wait = 0.3f;
						return;
					}
					int next = Mathf.Min(at + 1, vaultHart.Path.Count - 1);
					Vector2 ahead = (vaultHart.Path[next] - hart.GlobalPosition).Normalized();
					stand = hart.GlobalPosition - ahead * 200f;
					if (branchClock > 45f)
					{
						Check("the hart led him to the seat within 45 s", false);
						Finish();
					}
					return;
				}
				case 110:
				{
					Check($"he wandered {PaleHart.LeashRadius + 200f:0} px off part-way: the hart went home (stand {hart.State.Step})",
						hart.State.Step == 0 && hart.GlobalPosition.DistanceTo(vaultHart.Path[0]) < 2f);
					hops = 0;
					branchClock = 0f;
					step = 102;
					return;
				}
				case 103:
				{
					Check("it stands under the high seat", hart.GlobalPosition.DistanceTo(vaultHart.Seat) < 2f);
					Shot("the hart under the high seat, the bow lit");
					stand = vaultHart.Seat + new Vector2(0f, 40f);
					step++;
					wait = 0.25f;
					return;
				}
				case 104:
				{
					Check("touching the seat takes the bow's light", vaultHart.BowTaken);
					step = 6;
					wait = 1.8f;
					return;
				}
			}
		}
	}
}
