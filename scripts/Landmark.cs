using System;
using Godot;

/// <summary>
/// A rare, hand-made-looking place in the endless ground - for now the flower pen, a little wattle
/// fence round one glowing moonflower. Purely decorative today; built as a placement other systems
/// can find and hang behaviour on. See .ai/terrain.md section 8.
/// </summary>
/// <remarks>
/// THE HOOK, FOR WHOEVER BUILDS SECRETS. Three ways to find a landmark, all cheap:
///   - <see cref="Spawned"/> fires when one enters the tree (and <see cref="Despawned"/> when its
///     chunk is forgotten), so a system can keep its own list without polling the tree.
///   - Every landmark is in the "landmarks" group.
///   - <see cref="Key"/> is stable across visits AND across runs on the same terrain seed: it is
///     built from the stage, the chunk and the kind, and the chunk's placement is a pure function of
///     the seed (Node2DGame.PlanLandmark). Save "found" state against it.
/// <see cref="Centre"/> is the flower; <see cref="Bounds"/> is the fenced area in world pixels.
/// What a secret DOES - a boon for standing at the flower, a wizard who appears at the third one
/// found - belongs in that system, not here: this node owns placement and appearance only.
///
/// NO COLLISION, ON PURPOSE. The fence is knee-high wattle with a gate in its front rail, and it is
/// drawn so it sorts in front of an actor behind it and behind one in front of it - but it does not
/// stop anything. A solid pen is a corner a swarm can pin the player in, it would need the ghost
/// enemies' decor exceptions (Node2DGame.FurnishDecorSet), and nothing yet needs it to be solid. If
/// a secret ever wants the player to use the gate, give the rails a StaticBody2D then.
/// </remarks>
public partial class Landmark : Node2D
{
	/// <summary>Fired when a landmark enters the tree. The secrets hook - see the class remarks.</summary>
	public static event Action<Landmark> Spawned;

	/// <summary>Fired when a landmark leaves the tree, which is when its chunk is forgotten.</summary>
	public static event Action<Landmark> Despawned;

	public const string GroupName = "landmarks";

	/// <summary>What kind of landmark this is. "flower_pen" is the only one so far.</summary>
	public string Kind { get; set; } = "";

	/// <summary>Stable id: stage, chunk and kind. The same place has the same key every visit.</summary>
	public string Key { get; set; } = "";

	/// <summary>The fenced area, in world pixels.</summary>
	public Rect2 Bounds { get; set; }

	/// <summary>The special thing at the middle - the moonflower - in world pixels.</summary>
	public Vector2 Centre { get; set; }

	public override void _EnterTree()
	{
		AddToGroup(GroupName);
		Spawned?.Invoke(this);
	}

	public override void _ExitTree()
	{
		Despawned?.Invoke(this);
	}
}
