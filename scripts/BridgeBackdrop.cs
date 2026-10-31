using Godot;
using WizardSurvivors.scripts;

// HIS CASTLE ON THE HORIZON (.ai/endgame-castle.md 3.4, item 2: "something pulling forward"). In three-
// quarter view the far distance is up the screen, and the only far distance the bridge shows is the drop:
// so the castle stands out there, seen across the water past the top-left parapet, rising out of a mist line
// near the top of the screen as he nears it. It MOVES and never scales - three drawings at three heights
// (tools/art/bridge_backdrop.py), swapped as he passes a third and two thirds of the way, each rising out of
// the mist a whole pixel at a time across its third - so it stays on the art's own pixel grid, as every
// sprite in the game does.
//
// It is drawn only over the drop. A shader (the same band BridgeBand gives the generator, as uniforms)
// throws away every pixel over the deck and a margin past its shore, so the castle never stands on the
// bridge, under an enemy, or anywhere the player has to read; it is a shape in the mist, nothing more. Its
// place on screen follows the camera: the right end of its drawing is kept just short of where the top-left
// shore crosses it, the part of the drop nearest the way ahead.
public partial class BridgeBackdrop : Node2D
{
	/// <summary>The rules it reads the bridge and the player from. Set before it enters the tree.</summary>
	public BridgeRules Rules { get; set; }

	private const string Dir = "res://assets/bonelight/world/bridge/";
	private static readonly string[] Heights = { "castle_far", "castle_mid", "castle_near" };

	private readonly Texture2D[] textures = new Texture2D[3];
	private Sprite2D sprite;
	private ShaderMaterial material;
	private int shown = -1;

	// The mist line it rises out of, this far below the top of the screen (world px): deep enough that the
	// nearest drawing, risen whole, still stands below the top edge.
	private const float MistDepth = 200f;
	// How much of the drawing stands above the mist at the start of each third and at its end.
	private const float RiseFrom = 0.4f;
	private const float RiseTo = 1.0f;
	// Kept this far left of where the top-left shore crosses the castle's middle, so it stands over water.
	private const float ShoreClearance = 24f;

	public override void _Ready()
	{
		ZAsRelative = false;
		// Over the painted ground and the water (the terrain is at -100), under everything else in the world.
		ZIndex = DrawLayers.Terrain + 40;
		TextureFilter = TextureFilterEnum.Nearest;
		for (int i = 0; i < Heights.Length; i++)
			textures[i] = GD.Load<Texture2D>(Dir + Heights[i] + ".png");
		if (textures[0] == null)
		{
			GD.PushError("BridgeBackdrop: the castle drawings are not imported; nothing on the horizon. Run: python tools/art/bridge_backdrop.py, then import.");
			return;
		}

		material = new ShaderMaterial { Shader = GD.Load<Shader>("res://scenes/shaders/bridge_backdrop.gdshader") };
		var starts = new float[16];
		var halves = new float[16];
		BridgeBand.Piece[] pieces = BridgeBand.Pieces;
		for (int i = 0; i < 16; i++)
		{
			starts[i] = i < pieces.Length ? pieces[i].S0 : 1e9f;
			halves[i] = i < pieces.Length ? pieces[i].HalfWidthPx : 0f;
		}
		material.SetShaderParameter("seg_s", starts);
		material.SetShaderParameter("seg_hw", halves);
		material.SetShaderParameter("seg_count", pieces.Length);
		material.SetShaderParameter("start_s", BridgeBand.Start);
		material.SetShaderParameter("end_s", BridgeBand.End);
		sprite = new Sprite2D { Centered = false, Material = material, TextureFilter = TextureFilterEnum.Nearest };
		AddChild(sprite);
	}

	public override void _Process(double delta)
	{
		if (sprite == null || Rules?.Game == null || Rules.Game.RunPlayer is not Player player)
			return;
		var camera = player.GetNodeOrNull<Camera2D>("Camera2D");
		Vector2 centre = camera != null ? camera.GetScreenCenterPosition() : player.GlobalPosition;
		float zoom = camera != null && camera.Zoom.X > 0.01f ? camera.Zoom.X : 1f;
		Vector2 half = GetViewport().GetVisibleRect().Size / (2f * zoom);
		Vector2 origin = Rules.Game.StageOrigin;
		material.SetShaderParameter("origin", origin);

		float s = Rules.AlongOf(player.GlobalPosition);
		float progress = Mathf.Clamp((s - BridgeBand.Start) / (BridgeBand.End - BridgeBand.Start), 0f, 1f);
		int height = Mathf.Min(2, (int)(progress * 3f));
		if (height != shown)
		{
			shown = height;
			sprite.Texture = textures[height] ?? textures[0];
		}
		Texture2D tex = sprite.Texture;
		if (tex == null)
			return;
		Vector2 size = tex.GetSize();

		// It rises out of the mist: within each third, from RiseFrom of the drawing showing to all of it.
		float top = centre.Y - half.Y;
		float mist = Mathf.Round(top + MistDepth);
		float third = progress * 3f - height;
		float showing = Mathf.Lerp(RiseFrom, RiseTo, Mathf.Clamp(third, 0f, 1f)) * size.Y;
		float bottom = mist + (size.Y - showing);
		material.SetShaderParameter("mist_y", mist);
		// Where the top-left shore (t = -half-width) crosses the castle's middle height: x + y = c there.
		float hw = BridgeBand.HalfWidthAt(Mathf.Clamp(s, BridgeBand.Start, BridgeBand.End - 1f));
		float c = origin.X + origin.Y - hw * 1.41421356f;
		float shoreX = c - (mist - showing * 0.5f);
		// Whole pixels: the drawing moves, it never lands between them.
		Position = new Vector2(Mathf.Round(shoreX - ShoreClearance - size.X), Mathf.Round(bottom - size.Y));
	}
}
