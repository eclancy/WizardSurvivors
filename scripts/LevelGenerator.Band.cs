namespace WizardSurvivors.scripts;

// THE BAND TOPOLOGY: the Last Bridge's ground (.ai/endgame-castle.md 3.2). A STUB, and piece C1's file.
//
// What it is to become: measure each world tile along the bridge's axis (s, north-east - up and right on
// both canvases) and across it (t); anything farther across than the deck's half-width at that s is the
// drop, generated as water so it is drawn from its shape and collides at its shoreline (.ai/terrain.md
// section 3); the spans, gatehouse narrows, yards and the gate yard are widths along s. A pure function
// of the world tile, like every topology (LevelGenerator, "Topologies in partial files").
//
// How to build it: give BuildBandTopology a body that sets build.Generate (and leaves build.Walled
// false: the drop is water, not wall). FillOpenGround(grid, chunk) gives the deck its floor and blobs
// first, if wanted. Until build.Generate is set the Last Bridge is generated as Open, which is what it
// plays on today - and nothing else needs an edit for the day it is set: LevelGenerator.Describe reports
// Band, and RegressionChecks.ValidateTerrainSeams starts holding it to the seam rule by itself.
public sealed partial class LevelGenerator
{
	static partial void BuildBandTopology(ref TopologyBuild build)
	{
		// Not built: Generate stays null, and the bridge is open ground (C1 replaces this body).
	}
}
