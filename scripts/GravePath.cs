using Godot;
using System;

namespace WizardSurvivors.scripts;

// Gravewell's Grave Line (GravewellTrap.GraveLineId, 2026-10-29): the ground the wizard has just crossed,
// remembered so the graves can be dug along it - a minefield behind him for whatever follows.
//
// A child of the Player, made by Player.FireGravewellTrap the first time Grave Line casts, that drops a
// sample every SampleSpacing he walks into a fixed ring (Samples of them, about a screen of path). Laid
// back from the newest sample, the line follows the way he actually went, turns and all, rather than a
// straight line behind his back. Nothing allocated after it is made.
public partial class GravePath : Node2D
{
	private const int Samples = 48;
	private const float SampleSpacing = 20f;

	private readonly Vector2[] ring = new Vector2[Samples];
	private int head;
	private int count;
	private Node2D owner;

	public override void _Ready()
	{
		Name = "GravePath";
		owner = GetParent<Node2D>();
	}

	public override void _Process(double delta)
	{
		if (owner == null || !IsInstanceValid(owner))
			return;
		Vector2 at = owner.GlobalPosition;
		if (count > 0 && at.DistanceSquaredTo(ring[(head - 1 + Samples) % Samples]) < SampleSpacing * SampleSpacing)
			return;
		ring[head] = at;
		head = (head + 1) % Samples;
		count = Math.Min(Samples, count + 1);
	}

	/// <summary>
	/// Fills <paramref name="spots"/> with up to <paramref name="want"/> points along the path behind the
	/// wizard, the first <paramref name="first"/> back along it from where he stands and each next
	/// <paramref name="spacing"/> further. Returns how many the remembered path was long enough for.
	/// </summary>
	public int Behind(Vector2 from, float first, float spacing, int want, Vector2[] spots)
	{
		int found = 0;
		float walked = 0f;
		float next = first;
		Vector2 prev = from;
		for (int k = 1; k <= count && found < want && found < spots.Length; k++)
		{
			Vector2 p = ring[(head - k + Samples) % Samples];
			float seg = prev.DistanceTo(p);
			while (seg > 0.01f && walked + seg >= next && found < want && found < spots.Length)
			{
				spots[found++] = prev.Lerp(p, (next - walked) / seg);
				next += spacing;
			}
			walked += seg;
			prev = p;
		}
		return found;
	}
}
