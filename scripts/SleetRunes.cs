using Godot;
using System;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Sleet Runes (Ice + Wind): runes of rime cast the way the player is facing, a fast stream of them
// carried on the wind, aimed at nothing.
//
// WHY IT EXISTS (.ai/spell-roster.md, "The facing family"). The genre's thrown projectile, in the
// developer's words "spells themed around magic ... that operate similarly": a true projectile that goes
// where the player last walked (Player.FacingDirection), never at an enemy. It is the facing family's
// ranged half - Stormsweep covers the ground beside him, this the line in front of him - and what it
// asks is the same: walking is aiming. A player backing away from a crowd casts away from it, so he has
// to turn into what he is fleeing for a moment, and that is the decision the spell is.
//
// CADENCE. The cooldown is a cast: it hands the node the spell's runes (the count plus Amount), and the
// node lets them go one after another, StreamGap apart, each along the facing AT ITS RELEASE - so a
// stream bends as he turns, and a rune cast while he stands still goes where he last walked. A tight
// stream, not a spread: a small alternating offset across the line so the runes read as several, never
// a fan (the fan is Sigil Fan, a level 8 ascension).
//
// A TRUE PROJECTILE. Each rune flies straight at its speed for its range (the spell's Range), strikes
// what its path crosses - the segment it moved this frame, widened by its own HitRadius and the body it
// meets (RiptideWave.BodyAllowance), so a fast rune at a low frame rate cannot step over a body - and
// passes through Pierce + 1 enemies before it is spent. No enemy is struck twice by one rune on one
// leg. Pierce and speed are real numbers here, which is what this kind of spell is for.
//
// ONE NODE, EVERY RUNE. The node lives in the arena at the origin and draws in world space; runes,
// their hit lists and their sparks are slots in fixed arrays, and a rune is one region of a pre-rotated
// sheet (tools/art/sleet_runes.py, 32 headings) drawn in one call - nothing instanced or allocated per
// rune, which is what lets a level 8 stream of dozens fly on a phone.
//
// THE ANIMATION IS THE HIT AREA. A rune is its own hit: the glyph, and the body width it is tested at.
// Where it strikes, rime flakes are thrown out to exactly that width; where it is spent or runs out of
// range, it dissolves into sleet. No marker anywhere (.ai/art-direction.md section 3).
//
// The milestones (SpellEvolutionCatalog.SetupSleetRunesEvolutions), every one a change on screen
// (.ai/spell-roster.md section 2h):
//   Great Rune   (level 4, +1 Ice) - every GreatEvery-th rune is cut deep: a great rune twice the size
//                (its own sheet), slower, through everything in its line, slowing what it strikes.
//   Braided Wind (level 4) - the wind weaves the stream: every rune snakes from side to side as it
//                flies, each out of step with the last, so the stream braids across a wider band.
//   Parting Wind (level 4) - what a rune strikes is thrown aside, off its line, so the stream cuts a
//                lane through the pack.
//   Sigil Fan    (level 8) - every cast leaves at once, across a fan, and more of them.
//   Turning Gale (level 8, +1 Wind) - at the end of its flight the wind turns every rune and it flies
//                back to the player, striking again on the way.
public partial class SleetRunes : Node2D
{
	// Evolution ids. SpellEvolutionCatalog builds the options from these; this node reads them. The three
	// level 4 cards were Deep Rime (+35% and a pierce), Rune Flurry (+2 runes) and Far Wind (faster and
	// farther) until phase 2 (2026-10-29): numbers only. They kept their ids, so a save's seen milestones
	// carry over.
	public const string GreatRuneId = "sleet_runes_deep_rime";
	public const string BraidedWindId = "sleet_runes_rune_flurry";
	public const string PartingWindId = "sleet_runes_far_wind";
	public const string SigilFanId = "sleet_runes_sigil_fan";
	public const string TurningGaleId = "sleet_runes_turning_gale";

	/// <summary>The most runes one cast may hand over. Amount can push a count past this.</summary>
	public const int MaxRunesPerCast = 16;

	/// <summary>Headings in the rune sheet (tools/art/sleet_runes.py DIRECTIONS). An API: change both.</summary>
	public const int Directions = 32;

	// --- the rune (tuned in scenes/SleetRunes.tscn) ----------------------------------------------
	[Export] public Texture2D RuneSheet { get; set; }
	// Great Rune's sheet: the same rune drawn again at GreatScale on a cell twice as wide
	// (tools/art/sleet_runes.py), so its pixels stay the world's 2 px ones.
	[Export] public Texture2D GreatRuneSheet { get; set; }
	// px a second at level 1, before the levels' ProjectileSpeed lines and milestones' SpeedMultiplier.
	[Export] public float BaseSpeed { get; set; } = 640f;
	// The rune's own half-width as a hit. The body it meets adds RiptideWave.BodyAllowance.
	[Export] public float HitRadius { get; set; } = 6f;
	// Seconds between the runes of one cast.
	[Export] public float StreamGap { get; set; } = 0.05f;
	// Where a rune leaves the player: this far out along the facing.
	[Export] public float HandReach { get; set; } = 18f;

	// --- Great Rune ---------------------------------------------------------------------------------
	// Every GreatEvery-th rune let go (counted across casts, so a short cast still sends its share) is a
	// great rune: drawn 1.9 times the size (the great sheet), hit GreatHitScale times as wide, flying
	// GreatSpeed of a rune's speed, striking for GreatDamage of a rune, through everything in its line
	// (its hit list's size), and slowing what it strikes to GreatSlow for GreatSlowSeconds.
	public const int GreatEvery = 4;
	public const float GreatHitScale = 2.2f;
	public const float GreatSpeed = 0.7f;
	public const float GreatDamage = 1.5f;
	public const float GreatSlow = 0.7f;
	public const float GreatSlowSeconds = 1.5f;

	// --- Braided Wind -------------------------------------------------------------------------------
	// A rune snakes WeaveAmplitude either side of its line, once every WeaveWavelength of flight, and the
	// next rune is half a wave out of step, so two of them cross twice a wave - a braid. It leaves the
	// hand on the line and swings out over WeaveRamp.
	public const float WeaveAmplitude = 18f;
	public const float WeaveWavelength = 170f;
	private const float WeaveRamp = 40f;

	// --- Parting Wind -------------------------------------------------------------------------------
	// What a rune strikes is thrown off its line, to the side of it the body stands on, at PartShove
	// (Enemy.ApplyKnockback; a boss's resistance scales it). The flakes of the strike fly that way too.
	public const float PartShove = 190f;

	// --- Sigil Fan ----------------------------------------------------------------------------------
	public const float FanDegrees = 50f;

	// --- Turning Gale -------------------------------------------------------------------------------
	// How hard the wind turns a rune back toward the player, in multiples of its speed a second: at 14 it
	// reverses in about a seventh of a second - a turn the eye sees, not a bounce.
	public const float TurnRate = 14f;
	// A returning rune within this of the player is gathered back; one that has chased him this long is
	// let go.
	private const float CatchRadius = 22f;
	private const float MaxReturnSeconds = 1.6f;

	private const int MaxRunes = 96;
	// One rune's hit list, per leg. Pierce past this is not a number any build reaches but a great rune's,
	// which goes through everything in its line - this many, in practice a whole rank and the one behind.
	private const int MaxHitsPerRune = 32;
	private const int MaxSparks = 48;
	// Enemies one rune may meet on one frame's segment, sorted nearest first so Pierce stops where it
	// should. A frame's segment is 10 px at 60 fps; this is room for a packed crowd at a stutter.
	private const int MaxCandidates = 12;
	// Gap between two cast sounds: at level 8 a stream is a rune every 0.05 s.
	private const int SoundGapMs = 45;
	private const float Body = RiptideWave.BodyAllowance;
	private const float SparkSeconds = 0.26f;
	private const float VanishSeconds = 0.14f;

	private struct Rune
	{
		public Vector2 Pos;        // world
		public Vector2 Vel;        // world px/s
		public float Speed;
		public float Travelled;
		public float Range;
		public float ReturnAge;    // seconds on the way back
		public int Damage;
		public int PierceLeft;     // enemies it may still pass through after the one it is striking
		public int MaxPierce;      // what a fresh leg starts with
		public int Hits;           // ids in this rune's row of hitIds, this leg
		public bool Returns;       // Turning Gale
		public bool Returning;     // on the way back
		public bool Great;         // Great Rune
		public bool Parts;         // Parting Wind
		public float Reach;        // half-width as a hit, before the body it meets
		// Braided Wind: the line it weaves about, the way along it, how far either side, and where in
		// the wave it starts. Weave 0 is a straight flight.
		public Vector2 Line;
		public Vector2 Dir;
		public float Weave;
		public float Phase;
		public bool Live;
	}

	private struct Spark
	{
		public Vector2 At;         // world
		public Vector2 Heading;
		public float Age;
		public uint Seed;
		public bool Vanish;        // a rune going out of range, not a strike
		public bool Great;         // a great rune's: its flakes reach its wider hit
		public bool Live;
	}

	public Player PlayerRef { get; set; }
	public SpellData SpellData { get; private set; }

	// The numbers of the last cast. Each rune takes its own when it is released.
	private int damage = 6;
	private float speed = 640f;
	private float range = 380f;
	private int pierce;
	private bool fan;
	private bool returns;
	private bool great;
	private bool braided;
	private bool parting;
	// Runes let go since the node was made: every GreatEvery-th is a great rune under Great Rune, and
	// odd and even weave out of step under Braided Wind.
	private int released;
	private Color tint = Colors.White;

	// The stream: runes owed by the last cast, and the clock that lets them go.
	private int owed;
	private int streamIndex;
	private float releaseClock;

	private readonly Rune[] runes = new Rune[MaxRunes];
	private readonly ulong[] hitIds = new ulong[MaxRunes * MaxHitsPerRune];
	private readonly Spark[] sparks = new Spark[MaxSparks];
	private readonly List<Enemy> nearBuffer = new();
	private readonly Enemy[] candidate = new Enemy[MaxCandidates];
	private readonly float[] candidateAt = new float[MaxCandidates];
	private readonly ElementColors.Ramp ice = ElementColors.GetRamp(Element.Ice);
	private readonly ElementColors.Ramp wind = ElementColors.GetRamp(Element.Wind);
	private int nextRune;
	private int nextSpark;
	private uint seedClock;
	private bool drewLastFrame;

	public override void _Ready()
	{
		// Over the bodies they fly among and the status marks on them (3), under the HUD's own layer: in
		// the air (DrawLayers).
		ZAsRelative = false;
		ZIndex = DrawLayers.Air;
	}

	/// <summary>
	/// One cooldown's cast: <paramref name="count"/> runes, let go one after another (or all at once
	/// across a fan under Sigil Fan). Called by the fire loop every cooldown, which is also what keeps
	/// the node current through level-ups and milestones.
	/// </summary>
	public void Cast(SpellData spell, int count, float damageMultiplier, Color visualTint)
	{
		if (spell == null)
			return;
		SpellData = spell;
		int level = Math.Max(1, spell.CurrentLevel);
		damage = Math.Max(1, Mathf.RoundToInt(spell.GetDamageAtLevel(level) * damageMultiplier));
		// The levels' ProjectileSpeed lines are px a second; a milestone's SpeedMultiplier scales the lot.
		speed = MathF.Max(120f, (BaseSpeed + spell.GetEffectValueAtLevel(SpellEffect.ProjectileSpeed, level))
			* spell.GetSpeedMultiplier());
		range = MathF.Max(80f, spell.GetRangeAtLevel(level));
		pierce = Math.Max(0, spell.GetPierceAtLevel(level));
		fan = spell.HasEvolution(SigilFanId);
		returns = spell.HasEvolution(TurningGaleId);
		great = spell.HasEvolution(GreatRuneId);
		braided = spell.HasEvolution(BraidedWindId);
		parting = spell.HasEvolution(PartingWindId);
		tint = visualTint;

		count = Math.Clamp(count, 1, MaxRunesPerCast);
		if (fan)
		{
			ReleaseFan(count);
			return;
		}
		// A backlog is capped at two casts, so a cooldown cut mid-stream cannot pour.
		bool idle = owed == 0;
		owed = Math.Min(owed + count, count * 2);
		if (idle)
		{
			streamIndex = 0;
			releaseClock = 0f;
			ReleaseNext();
		}
	}

	public override void _Process(double delta)
	{
		var perfStart = FrameProfiler.Begin();
		float dt = (float)delta;
		if (owed > 0)
		{
			releaseClock += dt;
			while (owed > 0 && releaseClock >= StreamGap)
			{
				releaseClock -= StreamGap;
				ReleaseNext();
			}
		}

		bool any = false;
		for (int i = 0; i < MaxRunes; i++)
		{
			if (!runes[i].Live)
				continue;
			any = true;
			Fly(i, dt);
		}
		for (int s = 0; s < MaxSparks; s++)
		{
			if (!sparks[s].Live)
				continue;
			any = true;
			sparks[s].Age += dt;
			if (sparks[s].Age >= (sparks[s].Vanish ? VanishSeconds : SparkSeconds))
				sparks[s].Live = false;
		}
		// Once more after the last one goes, or its final frame would stay on screen with nothing to
		// clear it: a canvas item keeps what it last drew until it is redrawn.
		if (any || drewLastFrame)
			QueueRedraw();
		drewLastFrame = any;
		FrameProfiler.End(FrameProfiler.Section.SleetRunes, perfStart);
	}

	// --- releasing ---------------------------------------------------------------------------------

	private Vector2 Facing() =>
		PlayerRef != null && IsInstanceValid(PlayerRef) ? PlayerRef.FacingDirection : Vector2.Right;

	private Vector2 Hand(Vector2 heading) =>
		(PlayerRef != null && IsInstanceValid(PlayerRef) ? PlayerRef.GlobalPosition : GlobalPosition) + heading * HandReach;

	// The next rune of the stream, along the facing as it is NOW. Every other rune steps a little off
	// the line, alternating sides, so a stream reads as several runes rather than one long streak.
	private void ReleaseNext()
	{
		owed--;
		Vector2 heading = Facing();
		int k = streamIndex++;
		float offset = k == 0 ? 0f : ((k & 1) == 1 ? 1f : -1f) * (4f + 2f * ((k - 1) / 2 % 2));
		Vector2 across = new Vector2(-heading.Y, heading.X);
		Release(Hand(heading) + across * offset, heading);
	}

	// Sigil Fan: the whole cast at once, spread evenly across FanDegrees about the facing.
	private void ReleaseFan(int count)
	{
		Vector2 heading = Facing();
		float spread = Mathf.DegToRad(FanDegrees);
		for (int k = 0; k < count; k++)
		{
			float turn = count > 1 ? -spread * 0.5f + spread * k / (count - 1) : 0f;
			Vector2 dir = heading.Rotated(turn);
			Release(Hand(dir), dir);
		}
	}

	private void Release(Vector2 at, Vector2 heading)
	{
		int slot = -1;
		for (int i = 0; i < MaxRunes; i++)
		{
			int s = (nextRune + i) % MaxRunes;
			if (!runes[s].Live)
			{
				slot = s;
				break;
			}
		}
		// Every slot in flight: the oldest is nearly spent, and one rune more or less in a storm of
		// ninety is invisible. The cap is what keeps the cost flat.
		if (slot < 0)
			slot = nextRune;
		nextRune = (slot + 1) % MaxRunes;

		released++;
		bool isGreat = great && released % GreatEvery == 0;
		float runeSpeed = isGreat ? speed * GreatSpeed : speed;
		// A great rune goes through everything in its line: its hit list's size, a whole rank and more.
		int runePierce = isGreat ? MaxHitsPerRune - 1 : pierce;
		runes[slot] = new Rune
		{
			Pos = at,
			Vel = heading * runeSpeed,
			Speed = runeSpeed,
			Range = range,
			Damage = isGreat ? Math.Max(1, Mathf.RoundToInt(damage * GreatDamage)) : damage,
			PierceLeft = runePierce,
			MaxPierce = runePierce,
			Returns = returns,
			Great = isGreat,
			Parts = parting,
			Reach = isGreat ? HitRadius * GreatHitScale : HitRadius,
			Line = at,
			Dir = heading,
			// A great rune is too heavy for the wind to weave; it ploughs straight on down the braid.
			Weave = braided && !isGreat ? WeaveAmplitude : 0f,
			Phase = (released & 1) == 0 ? 0f : Mathf.Pi,
			Live = true,
		};
		SfxPlayer.Instance?.PlayAt(SfxCatalog.SpellSleetRune, at, 0.08f, SfxCatalog.SpellSleetRune, SoundGapMs);
	}

	// --- flight -------------------------------------------------------------------------------------

	private void Fly(int i, float dt)
	{
		ref Rune k = ref runes[i];
		Vector2 from = k.Pos;

		if (k.Returning)
		{
			k.ReturnAge += dt;
			if (PlayerRef == null || !IsInstanceValid(PlayerRef) || k.ReturnAge >= MaxReturnSeconds)
			{
				Vanish(ref k);
				return;
			}
			Vector2 home = PlayerRef.GlobalPosition;
			Vector2 toHome = home - k.Pos;
			if (toHome.LengthSquared() <= CatchRadius * CatchRadius)
			{
				// Gathered back into the hand, gone without a mark.
				k.Live = false;
				return;
			}
			k.Vel = k.Vel.MoveToward(toHome.Normalized() * k.Speed, k.Speed * TurnRate * dt);
		}

		if (k.Weave > 0f && !k.Returning)
		{
			// Braided Wind: the line moves on at the rune's speed, and the rune swings about it. Its range
			// is measured along the line, so a braided stream reaches as far as a straight one.
			k.Line += k.Dir * k.Speed * dt;
			k.Travelled += k.Speed * dt;
			Vector2 across = new Vector2(-k.Dir.Y, k.Dir.X);
			float swing = k.Weave * Smooth(k.Travelled / WeaveRamp)
				* MathF.Sin(Mathf.Tau * k.Travelled / WeaveWavelength + k.Phase);
			Vector2 next = k.Line + across * swing;
			if (dt > 0.00001f)
				k.Vel = (next - k.Pos) / dt;
			k.Pos = next;
		}
		else
		{
			k.Pos += k.Vel * dt;
			k.Travelled += k.Vel.Length() * dt;
		}
		Strike(ref k, i, from, k.Pos);
		if (!k.Live)
			return;

		if (!k.Returning && k.Travelled >= k.Range)
		{
			if (k.Returns)
			{
				// Turning Gale: a fresh leg - every enemy may be struck again on the way back, and it
				// passes through as many as it did going out. It comes home straight: the braid unwinds.
				k.Returning = true;
				k.Hits = 0;
				k.PierceLeft = k.MaxPierce;
				k.Weave = 0f;
				k.Vel = k.Dir * k.Speed;
			}
			else
				Vanish(ref k);
		}
	}

	private static float Smooth(float x)
	{
		x = Mathf.Clamp(x, 0f, 1f);
		return x * x * (3f - 2f * x);
	}

	// Everything the rune's path crossed this frame, nearest first, until its pierce is spent.
	private void Strike(ref Rune k, int i, Vector2 from, Vector2 to)
	{
		if (PlayerRef == null || !IsInstanceValid(PlayerRef))
			return;
		Vector2 path = to - from;
		float length = path.Length();
		float reach = k.Reach + Body;
		EnemyRegistry.CollectNear(from + path * 0.5f, length * 0.5f + reach, nearBuffer);
		if (nearBuffer.Count == 0)
			return;

		int row = i * MaxHitsPerRune;
		int found = 0;
		float reachSquared = reach * reach;
		float lengthSquared = MathF.Max(0.0001f, length * length);
		foreach (Enemy enemy in nearBuffer)
		{
			if (enemy == null || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			Vector2 p = enemy.GlobalPosition;
			float t = Mathf.Clamp((p - from).Dot(path) / lengthSquared, 0f, 1f);
			if (p.DistanceSquaredTo(from + path * t) > reachSquared)
				continue;
			ulong id = enemy.GetInstanceId();
			bool seen = false;
			for (int h = 0; h < k.Hits; h++)
			{
				if (hitIds[row + h] == id)
				{
					seen = true;
					break;
				}
			}
			if (seen)
				continue;
			// Insertion into the nearest-first list; past its size, the farthest is dropped.
			int at = Math.Min(found, MaxCandidates - 1);
			if (found == MaxCandidates && t >= candidateAt[at])
				continue;
			while (at > 0 && candidateAt[at - 1] > t)
			{
				candidate[at] = candidate[at - 1];
				candidateAt[at] = candidateAt[at - 1];
				at--;
			}
			candidate[at] = enemy;
			candidateAt[at] = t;
			if (found < MaxCandidates)
				found++;
		}
		nearBuffer.Clear();

		for (int c = 0; c < found; c++)
		{
			Enemy enemy = candidate[c];
			candidate[c] = null;
			if (!k.Live || enemy == null || !IsInstanceValid(enemy) || enemy.IsDying)
				continue;
			if (k.Hits < MaxHitsPerRune)
				hitIds[row + k.Hits++] = enemy.GetInstanceId();
			Vector2 flakes = k.Vel;
			if (k.Parts)
			{
				// Parting Wind: thrown off the rune's line, to the side it stands on (alternating for one
				// struck dead centre), and the rime flakes go with it.
				Vector2 heading = k.Vel.LengthSquared() > 0.0001f ? k.Vel.Normalized() : Vector2.Right;
				Vector2 across = new Vector2(-heading.Y, heading.X);
				float side = (enemy.GlobalPosition - k.Pos).Dot(across);
				Vector2 shove = across * (MathF.Abs(side) > 1f ? MathF.Sign(side) : ((k.Hits & 1) == 0 ? 1f : -1f));
				enemy.ApplyKnockback(shove * PartShove);
				flakes = shove;
			}
			AddSpark(enemy.GlobalPosition, flakes, false, k.Great);
			PlayerRef.DealDamageToEnemy(enemy, k.Damage, source: SpellData);
			if (k.Great)
				enemy.ApplySlow(GreatSlow, GreatSlowSeconds);
			if (k.PierceLeft-- <= 0)
			{
				// Spent: it shatters in the body it stopped in.
				k.Live = false;
			}
		}
		for (int c = found; c < MaxCandidates; c++)
			candidate[c] = null;
	}

	private void Vanish(ref Rune k)
	{
		k.Live = false;
		AddSpark(k.Pos, k.Vel, true, k.Great);
	}

	private void AddSpark(Vector2 at, Vector2 heading, bool vanish, bool greatRune = false)
	{
		int slot = -1;
		for (int s = 0; s < MaxSparks; s++)
		{
			int q = (nextSpark + s) % MaxSparks;
			if (!sparks[q].Live)
			{
				slot = q;
				break;
			}
		}
		if (slot < 0)
			slot = nextSpark;
		nextSpark = (slot + 1) % MaxSparks;
		sparks[slot] = new Spark
		{
			At = at,
			Heading = heading.LengthSquared() > 0.0001f ? heading.Normalized() : Vector2.Right,
			Seed = ++seedClock,
			Vanish = vanish,
			Great = greatRune,
			Live = true,
		};
	}

	// --- drawing ------------------------------------------------------------------------------------

	public override void _Draw()
	{
		var perfStart = FrameProfiler.Begin();
		Transform2D toLocal = GlobalTransform.AffineInverse();
		DrawSparks(toLocal);
		DrawRunes(toLocal);
		FrameProfiler.End(FrameProfiler.Section.SleetRunes, perfStart);
	}

	private Color Ink(Color c, float alpha) => new Color(c.R * tint.R, c.G * tint.G, c.B * tint.B, alpha * tint.A);

	private void DrawRunes(Transform2D toLocal)
	{
		Color windMid = Ink(wind.Mid, 0.55f);
		Color windEdge = Ink(wind.Edge, 0.4f);
		for (int i = 0; i < MaxRunes; i++)
		{
			if (!runes[i].Live)
				continue;
			Rune k = runes[i];
			Vector2 at = toLocal * k.Pos;
			Vector2 heading = k.Vel.LengthSquared() > 0.0001f ? k.Vel.Normalized() : Vector2.Right;

			// The wind that carries it: two thin streaks behind its sleet tail, a step cooler than the rune
			// (twice as far back and apart behind a great rune).
			float size = k.Great ? 2f : 1f;
			Vector2 across = new Vector2(-heading.Y, heading.X);
			Vector2 tail = at - heading * 20f * size;
			DrawLine(Snap(tail + across * 4f * size), Snap(tail + across * 4f * size - heading * 14f * size), windMid, 2f);
			DrawLine(Snap(tail - across * 4f * size), Snap(tail - across * 4f * size - heading * 9f * size), windEdge, 2f);

			Texture2D sheet = k.Great && GreatRuneSheet != null ? GreatRuneSheet : RuneSheet;
			if (sheet == null)
				continue;
			float cell = sheet.GetHeight();
			int cellIndex = ((int)MathF.Round(heading.Angle() / Mathf.Tau * Directions) % Directions + Directions) % Directions;
			Vector2 corner = Snap(at - new Vector2(cell * 0.5f, cell * 0.5f));
			DrawTextureRectRegion(sheet, new Rect2(corner, new Vector2(cell, cell)),
				new Rect2(cellIndex * cell, 0f, cell, cell), tint);
		}
	}

	// A strike throws rime flakes forward through the body, out to exactly the width the rune was tested
	// at; a rune at the end of its flight dissolves into a puff of sleet in Wind's ramp.
	private void DrawSparks(Transform2D toLocal)
	{
		for (int s = 0; s < MaxSparks; s++)
		{
			if (!sparks[s].Live)
				continue;
			Spark spark = sparks[s];
			Vector2 at = toLocal * spark.At;
			float reach = (spark.Great ? HitRadius * GreatHitScale : HitRadius) + Body;
			if (spark.Vanish)
			{
				ImpactDebris.Draw(this, at, spark.Great ? 18f : 10f, spark.Age, spark.Seed, wind, spark.Great ? 10 : 6, tint,
					travel: 0.08f, life: VanishSeconds, streaks: false);
				continue;
			}
			ImpactDebris.Draw(this, at, reach, spark.Age, spark.Seed, ice, 7, tint,
				startAngle: spark.Heading.Angle() - 1.1f, sweep: 2.2f, life: SparkSeconds);
			if (spark.Age < 0.06f)
				DrawRect(WhipLimb.Cell(at, 6f), Ink(ice.Core, 1f - spark.Age / 0.06f));
		}
	}

	// WhipLimb is the project's 2 px art-grid helper; the name is the class's, not this spell's.
	private static Vector2 Snap(Vector2 p) =>
		new Vector2(MathF.Round(p.X / WhipLimb.Grid) * WhipLimb.Grid, MathF.Round(p.Y / WhipLimb.Grid) * WhipLimb.Grid);
}
