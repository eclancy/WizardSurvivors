using Godot;

namespace WizardSurvivors.scripts;

/// <summary>
/// A telegraphed circular hit centred on its owner: wait out a cooldown, grow a warning ring for
/// the wind-up, then damage whatever is standing inside the ring when it lands.
/// </summary>
/// <remarks>
/// <see cref="AttackTelegraph"/> was extracted when its wind-up clock had been written twice. This
/// is the layer above it, extracted for the same reason: the boss's ground slam, the slammer's, and
/// the exploder's detonation are the same promise to the player - a ring appears, and in a moment
/// everything inside it is hit - and three copies of that promise would be three chances to draw a
/// ring that does not match the damage.
///
/// The ring is the contract. <see cref="Radius"/> is both what <see cref="Draw"/> paints at full
/// wind-up and what <see cref="ResolveAgainst"/> measures against, so the two cannot drift apart.
/// </remarks>
public sealed class GroundSlamAttack
{
	/// <summary>Radius of both the warning ring and the hit it resolves.</summary>
	public float Radius { get; set; }

	/// <summary>Damage dealt to a target inside the ring when the slam lands.</summary>
	public int Damage { get; set; }

	/// <summary>Colour of the growing warning ring, so two slammers can be told apart mid-fight.</summary>
	public Color WarningColor { get; set; }

	private readonly AttackTelegraph telegraph;
	// Seconds left on the fading impact ring. Negative means there is nothing to draw.
	private float impactFlashRemaining = -1f;
	private const float ImpactFlashSeconds = 0.28f;

	public GroundSlamAttack(float intervalSeconds, float windUpSeconds, float radius, int damage, Color warningColor)
	{
		telegraph = new AttackTelegraph(intervalSeconds, windUpSeconds);
		Radius = radius;
		Damage = damage;
		WarningColor = warningColor;
	}

	/// <summary>Seconds between slams. Settable so a boss can speed its slam up on enrage.</summary>
	public float IntervalSeconds
	{
		get => telegraph.IntervalSeconds;
		set => telegraph.IntervalSeconds = value;
	}

	public bool IsWindingUp => telegraph.IsWindingUp;

	/// <summary>
	/// Advances the slam one frame and returns the beat, exactly as <see cref="AttackTelegraph"/>
	/// does: <c>Started</c> is the cue to plant the owner and play its swing, <c>Resolved</c> the
	/// frame to call <see cref="ResolveAgainst"/>. Also ages the impact ring, so a caller that
	/// draws needs no second timer of its own.
	/// </summary>
	public AttackTelegraph.Beat Tick(float delta, bool wantsToSlam)
	{
		if (impactFlashRemaining >= 0f)
			impactFlashRemaining -= delta;

		return telegraph.Tick(delta, wantsToSlam);
	}

	/// <summary>
	/// Lands the slam. Lights the impact ring whether or not anyone was caught - a slam that hit
	/// empty ground still has to look like it happened - and damages <paramref name="target"/> only
	/// if it is genuinely inside <see cref="Radius"/>. Returns whether the target was hit.
	/// </summary>
	/// <remarks>
	/// Duck-typed like every other damage call in this project: a slam does not need to know what a
	/// Player is, only that the thing it is standing on can take a hit.
	/// </remarks>
	public bool ResolveAgainst(Node2D owner, Node2D target)
	{
		impactFlashRemaining = ImpactFlashSeconds;

		if (owner == null || target == null || !GodotObject.IsInstanceValid(target))
			return false;

		if (owner.GlobalPosition.DistanceTo(target.GlobalPosition) > Radius)
			return false;

		if (!target.HasMethod("TakeDamage"))
			return false;

		target.Call("TakeDamage", Damage);
		return true;
	}

	/// <summary>
	/// Paints the warning ring while winding up, then the fading impact ring. Call from the owner's
	/// <c>_Draw</c>; the geometry allocates nothing, which matters in a scene that already has a
	/// swarm in it.
	/// </summary>
	public void Draw(CanvasItem canvas)
	{
		if (canvas == null)
			return;

		if (telegraph.IsWindingUp)
		{
			// Grows from a third of the radius to the full circle, so "how much longer" and "how far
			// out" are the same reading. The player answers it by stepping past the edge.
			float progress = telegraph.WindUpProgress;
			float radius = Mathf.Lerp(Radius * 0.35f, Radius, progress);
			var warning = new Color(WarningColor.R, WarningColor.G, WarningColor.B, Mathf.Lerp(0.35f, 0.85f, progress));
			canvas.DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 48, warning, 3.5f, true);
			canvas.DrawCircle(Vector2.Zero, radius, new Color(WarningColor.R, WarningColor.G, WarningColor.B, 0.10f));
			return;
		}

		if (impactFlashRemaining > 0f)
		{
			float fade = Mathf.Clamp(impactFlashRemaining / ImpactFlashSeconds, 0f, 1f);
			canvas.DrawArc(Vector2.Zero, Radius, 0f, Mathf.Tau, 48, new Color(1.0f, 0.85f, 0.55f, fade), 6f * fade, true);
		}
	}
}
