using Godot;

/// <summary>
/// The light that goes up from an Arcane Upgrade's socket when a level of it is bought: a flash in
/// the glass, a ring that opens out of the bezel, and eight motes thrown clear of it.
/// </summary>
/// <remarks>
/// A full-rect child of the card, last in its draw order bar the focus ring, so it draws over the
/// page it bursts from; <see cref="Burst"/> takes the socket's centre in the card's own coordinates.
/// Half a second, eased out, and it never takes input - the player can buy the next level while it
/// is still in the air, and the next burst simply restarts it.
///
/// The colours are the arcane ramp (the Arcane Energy the level was bought with) with gold for the
/// motes, from art-direction.md's element table: core #FFF0FF, hot #E8C0FF, mid #C08CFF. Rings and
/// motes grow a whole pixel at a time. Processes only while it is in the air.
/// </remarks>
public partial class ShopFlare : Control
{
	private const float Seconds = 0.5f;
	private static readonly Color Core = new Color(1.0f, 0.941f, 1.0f);
	private static readonly Color Hot = new Color(0.910f, 0.753f, 1.0f);
	private static readonly Color Mid = new Color(0.753f, 0.549f, 1.0f);
	private static readonly Color Mote = new Color(1.0f, 0.941f, 0.753f);  // gold hi

	private Vector2 centre;
	private ulong startMs;
	private bool live;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		FocusMode = FocusModeEnum.None;
		SetProcess(false);
	}

	/// <summary>Bursts at <paramref name="at"/>, in this control's coordinates.</summary>
	public void Burst(Vector2 at)
	{
		centre = at.Round();
		startMs = Time.GetTicksMsec();
		live = true;
		SetProcess(true);
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if ((Time.GetTicksMsec() - startMs) / 1000f >= Seconds)
		{
			live = false;
			SetProcess(false);
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (!live)
			return;

		float t = Mathf.Clamp((Time.GetTicksMsec() - startMs) / 1000f / Seconds, 0f, 1f);
		float ease = 1f - (1f - t) * (1f - t);
		float fade = 1f - t;

		// The flash in the glass: bright at once, gone by a third of the way.
		if (t < 0.33f)
			DrawCircle(centre, 30f, new Color(Hot, 0.45f * (1f - t / 0.33f)));

		// The ring out of the bezel, a whole pixel at a time.
		float radius = Mathf.Round(34f + ease * 26f);
		DrawArc(centre, radius, 0f, Mathf.Tau, 48, new Color(Mid, 0.85f * fade), 3f);
		DrawArc(centre, radius - 4f, 0f, Mathf.Tau, 48, new Color(Core, 0.5f * fade), 1f);

		// Eight motes thrown clear, square like every pixel in this game.
		for (int i = 0; i < 8; i++)
		{
			float a = i * Mathf.Tau / 8f + Mathf.Pi / 8f;
			Vector2 at = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Mathf.Round(30f + ease * 40f);
			DrawRect(new Rect2(at.Round() - new Vector2(2f, 2f), new Vector2(4f, 4f)), new Color(i % 2 == 0 ? Mote : Core, fade));
		}
	}
}
