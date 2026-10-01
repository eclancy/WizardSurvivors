using Godot;

namespace WizardSurvivors.scripts;

/// <summary>What shape the warning is drawn in, and therefore what shape gets hit.</summary>
/// <remarks>
/// A ring says "get out"; it cannot say "step sideways rather than backwards". A cone and a line
/// can, and a telegraph that carries which WAY to dodge is the difference between an attack the
/// player learns and one they only survive.
///
/// The drawn shape and the tested shape are the same numbers in every case. That is the whole
/// contract of <see cref="GroundSlamAttack"/>, and adding shapes must not weaken it - which is why
/// <see cref="GroundSlamAttack.Contains"/> is the single place any shape is decided, and both the
/// hit and the paint go through it rather than each re-deriving the geometry.
/// </remarks>
public enum TelegraphShape
{
	/// <summary>Centred on the caster. Dodged by leaving.</summary>
	Ring,
	/// <summary>A wedge along <see cref="GroundSlamAttack.Facing"/>. Dodged by stepping aside.</summary>
	Cone,
	/// <summary>A lane along <see cref="GroundSlamAttack.Facing"/>. Dodged by stepping aside.</summary>
	Line,
}

/// <summary>
/// A telegraphed hit anchored on its owner: wait out a cooldown, grow a warning shape for the
/// wind-up, then damage whatever is standing inside it when it lands.
/// </summary>
/// <remarks>
/// <see cref="AttackTelegraph"/> was extracted when its wind-up clock had been written twice. This
/// is the layer above it, extracted for the same reason: the boss's ground slam, the slammer's, and
/// the exploder's detonation are the same promise to the player - a warning appears, and in a
/// moment everything inside it is hit - and three copies of that promise would be three chances to
/// draw a shape that does not match the damage.
///
/// The shape is the contract. <see cref="Radius"/> is both what <see cref="Draw"/> paints at full
/// wind-up and what <see cref="ResolveAgainst"/> measures against, so the two cannot drift apart.
///
/// It resolves at the owner's position on the frame it lands, not at the position the wind-up
/// began. That is deliberate and it is what makes a travelling telegraph possible: a boss that
/// keeps moving through its own wind-up carries the warning with it, so a charge is this class
/// plus a caller that simply does not plant. Nothing here needed changing for that.
/// </remarks>
public sealed class GroundSlamAttack
{
	/// <summary>Reach of both the warning and the hit it resolves: ring radius, cone or lane length.</summary>
	public float Radius { get; set; }

	/// <summary>Ring by default, so every existing caller behaves exactly as before.</summary>
	public TelegraphShape Shape { get; set; } = TelegraphShape.Ring;

	/// <summary>Half-angle of a <see cref="TelegraphShape.Cone"/>. Ignored by the other shapes.</summary>
	public float HalfAngleDegrees { get; set; } = 32f;

	/// <summary>Half-width of a <see cref="TelegraphShape.Line"/>. Ignored by the other shapes.</summary>
	public float HalfWidth { get; set; } = 26f;

	/// <summary>
	/// Direction a Cone or Line points. The caster sets it on <see cref="AttackTelegraph.Beat.Started"/>
	/// and then leaves it alone: an aim that keeps tracking through the wind-up is not a telegraph,
	/// because there is no sideways to step to.
	/// </summary>
	public Vector2 Facing { get; set; } = Vector2.Right;

	/// <summary>Damage dealt to a target inside the shape when the attack lands.</summary>
	public int Damage { get; set; }

	/// <summary>Colour of the growing warning, so two attackers can be told apart mid-fight.</summary>
	public Color WarningColor { get; set; }

	private readonly AttackTelegraph telegraph;
	// Seconds left on the fading impact shape. Negative means there is nothing to draw.
	private float impactFlashRemaining = -1f;
	private const float ImpactFlashSeconds = 0.28f;

	// Cone fan and lane quad, allocated once. This draws every frame of every wind-up in a scene
	// that already has a swarm in it, and a per-frame Vector2[] per attacker is exactly the kind of
	// allocation the project bans in swarm-heavy effects.
	private const int ConeSegments = 14;
	private readonly Vector2[] coneBuffer = new Vector2[ConeSegments + 2];
	private readonly Vector2[] quadBuffer = new Vector2[4];

	public GroundSlamAttack(float intervalSeconds, float windUpSeconds, float radius, int damage, Color warningColor)
	{
		telegraph = new AttackTelegraph(intervalSeconds, windUpSeconds);
		Radius = radius;
		Damage = damage;
		WarningColor = warningColor;
	}

	/// <summary>Seconds between attacks. Settable so a boss can speed its slam up on enrage.</summary>
	public float IntervalSeconds
	{
		get => telegraph.IntervalSeconds;
		set => telegraph.IntervalSeconds = value;
	}

	/// <summary>Seconds of visible wind-up. Settable so a later phase can shorten its tell.</summary>
	public float WindUpSeconds
	{
		get => telegraph.WindUpSeconds;
		set => telegraph.WindUpSeconds = value;
	}

	public bool IsWindingUp => telegraph.IsWindingUp;

	/// <summary>Clears the cooldown so the next <see cref="Tick"/> starts winding up at once.</summary>
	public void Prime() => telegraph.Prime();

	/// <summary>0 the frame the wind-up starts, 1 the frame before it resolves.</summary>
	public float WindUpProgress => telegraph.WindUpProgress;

	/// <summary>
	/// Advances the attack one frame and returns the beat, exactly as <see cref="AttackTelegraph"/>
	/// does: <c>Started</c> is the cue to aim and play the swing, <c>Resolved</c> the frame to call
	/// <see cref="ResolveAgainst"/>. Also ages the impact flash, so a caller that draws needs no
	/// second timer of its own.
	/// </summary>
	public AttackTelegraph.Beat Tick(float delta, bool wantsToSlam)
	{
		if (impactFlashRemaining >= 0f)
			impactFlashRemaining -= delta;

		return telegraph.Tick(delta, wantsToSlam);
	}

	/// <summary>Points the Cone or Line at <paramref name="target"/>. A no-op for a Ring.</summary>
	public void AimAt(Vector2 origin, Vector2 target)
	{
		Vector2 offset = target - origin;
		if (offset.LengthSquared() > 0.0001f)
			Facing = offset.Normalized();
	}

	/// <summary>
	/// Whether <paramref name="point"/> is inside the attack, in world space. The single authority
	/// on every shape: both the damage and the paint read it, so a shape cannot be drawn one size
	/// and hit at another.
	/// </summary>
	public bool Contains(Vector2 origin, Vector2 point)
	{
		Vector2 offset = point - origin;

		switch (Shape)
		{
			case TelegraphShape.Cone:
			{
				float distance = offset.Length();
				if (distance > Radius)
					return false;
				// Standing on the caster is inside every cone. Without this the angle of a
				// zero-length offset is undefined and a target dead-centre survives point blank.
				if (distance <= 0.001f)
					return true;
				return Mathf.Abs(SafeFacing().AngleTo(offset)) <= Mathf.DegToRad(HalfAngleDegrees);
			}

			case TelegraphShape.Line:
			{
				// Deliberately NOT gated on the radius first. The far corners of a lane are further
				// from the caster than Radius, and rejecting them would shave the two most dangerous
				// pixels off a shape the player was shown in full.
				Vector2 facing = SafeFacing();
				float along = offset.Dot(facing);
				if (along < 0f || along > Radius)
					return false;
				return Mathf.Abs(offset.Dot(new Vector2(-facing.Y, facing.X))) <= HalfWidth;
			}

			default:
				return offset.Length() <= Radius;
		}
	}

	/// <summary>
	/// Lands the attack. Lights the impact flash whether or not anyone was caught - an attack that
	/// hit empty ground still has to look like it happened - and damages <paramref name="target"/>
	/// only if it is genuinely inside the shape. Returns whether the target was hit.
	/// </summary>
	/// <remarks>
	/// Duck-typed like every other damage call in this project: an attack does not need to know what
	/// a Player is, only that the thing standing in it can take a hit.
	/// </remarks>
	public bool ResolveAgainst(Node2D owner, Node2D target)
	{
		impactFlashRemaining = ImpactFlashSeconds;

		if (owner == null || target == null || !GodotObject.IsInstanceValid(target))
			return false;

		if (!Contains(owner.GlobalPosition, target.GlobalPosition))
			return false;

		if (!target.HasMethod("TakeDamage"))
			return false;

		// Weaken (issue #66) is read off the owner on the frame the hit lands. A TelegraphedGroundHit
		// owns its own shape, so the boss that placed it scaled the damage at Place() instead.
		int dealt = owner is Enemy enemy ? enemy.ScaleOutgoingDamage(Damage) : Damage;
		if (dealt <= 0)
			return false;

		target.Call("TakeDamage", dealt);
		return true;
	}

	/// <summary>
	/// Paints the warning while winding up, then the fading impact flash. Call from the owner's
	/// <c>_Draw</c>.
	/// </summary>
	/// <remarks>
	/// Drawn in the owner's local space, where the owner sits at the origin. <see cref="Facing"/> is
	/// a world direction and is used unrotated, which is correct for every actor in this game:
	/// enemies face by flipping their sprite, never by rotating the node, so local and world
	/// directions are the same thing here.
	/// </remarks>
	public void Draw(CanvasItem canvas)
	{
		if (canvas == null)
			return;

		if (telegraph.IsWindingUp)
		{
			// Grows from a third of the reach to the full shape, so "how much longer" and "how far
			// out" are the same reading. The player answers it by stepping past the edge.
			float progress = telegraph.WindUpProgress;
			float reach = Mathf.Lerp(Radius * 0.35f, Radius, progress);
			float alpha = Mathf.Lerp(0.35f, 0.85f, progress);
			DrawShape(canvas, reach, new Color(WarningColor.R, WarningColor.G, WarningColor.B, alpha), 3.5f);
			return;
		}

		if (impactFlashRemaining > 0f)
		{
			float fade = Mathf.Clamp(impactFlashRemaining / ImpactFlashSeconds, 0f, 1f);
			DrawShape(canvas, Radius, new Color(1.0f, 0.85f, 0.55f, fade), 6f * fade);
		}
	}

	private void DrawShape(CanvasItem canvas, float reach, Color edge, float width)
	{
		var fill = new Color(edge.R, edge.G, edge.B, edge.A * 0.16f);

		switch (Shape)
		{
			case TelegraphShape.Cone:
			{
				float half = Mathf.DegToRad(HalfAngleDegrees);
				float start = SafeFacing().Angle() - half;
				float sweep = half * 2f;

				coneBuffer[0] = Vector2.Zero;
				for (int i = 0; i <= ConeSegments; i++)
				{
					float a = start + sweep * i / ConeSegments;
					coneBuffer[i + 1] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * reach;
				}

				canvas.DrawColoredPolygon(coneBuffer, fill);
				canvas.DrawArc(Vector2.Zero, reach, start, start + sweep, ConeSegments * 2, edge, width, true);
				// The two straight edges matter more than the arc: they are the line the player has
				// to get across, and an unclosed wedge reads as a spotlight rather than as a hit.
				canvas.DrawLine(Vector2.Zero, coneBuffer[1], edge, width, true);
				canvas.DrawLine(Vector2.Zero, coneBuffer[ConeSegments + 1], edge, width, true);
				break;
			}

			case TelegraphShape.Line:
			{
				Vector2 facing = SafeFacing();
				Vector2 side = new Vector2(-facing.Y, facing.X) * HalfWidth;
				Vector2 end = facing * reach;

				quadBuffer[0] = side;
				quadBuffer[1] = end + side;
				quadBuffer[2] = end - side;
				quadBuffer[3] = -side;

				canvas.DrawColoredPolygon(quadBuffer, fill);
				for (int i = 0; i < 4; i++)
					canvas.DrawLine(quadBuffer[i], quadBuffer[(i + 1) % 4], edge, width, true);
				break;
			}

			default:
				canvas.DrawCircle(Vector2.Zero, reach, fill);
				canvas.DrawArc(Vector2.Zero, reach, 0f, Mathf.Tau, 48, edge, width, true);
				break;
		}
	}

	// A zero or denormalised facing would make every angle NaN and silently turn a cone into a hit
	// on nobody. Right is an arbitrary but stable fallback.
	private Vector2 SafeFacing() => Facing.LengthSquared() > 0.0001f ? Facing.Normalized() : Vector2.Right;
}
