namespace WizardSurvivors.scripts;

// THE AUTHORED TOPOLOGY: ground read from a file rather than a function (.ai/endgame-castle.md 5.1, 6.1).
// A STUB, and piece C3's file.
//
// What it is to become: the reader for tools/art/court_layout.py's court_layout.json (each cell's kind,
// each room's rectangle and id, each door and each hidden passage) - and, with the same reader, for
// throne_room.py's hall (piece C4). Cells outside the building are wall. A lookup is a pure function of
// the world tile, so the seam rule holds trivially. Which file a chapter reads is the chapter's (its
// StageEnvironmentKind is on the TopologyChunk), so one reader serves both.
//
// How to build it: give BuildAuthoredTopology a body that sets build.Generate and build.Walled = true
// (the halls have walls, so the Court plays by the maze's rules: no cleanup pass, every enemy on the flow
// field, spawns by walking distance). Set build.OwnsSpawnClearing if the layout's start must not be
// stamped open. Until build.Generate is set the Court and the Throne are generated as Open - nothing else
// needs an edit for the day it is set.
public sealed partial class LevelGenerator
{
	static partial void BuildAuthoredTopology(ref TopologyBuild build)
	{
		// Not built: Generate stays null, and the Court and the Throne are open ground (C3 replaces this body).
	}
}
