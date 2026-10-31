using Godot;
using System;
using System.Collections.Generic;
using WizardSurvivors.scripts;

// I2, THE BURIED RUNES - the Frozen Waste's spell vault (.ai/achievements-and-secrets.md 8.8). It pays
// Sleet Runes, the runes of rime he throws on the wind - learned from three cut under a drift, which the
// wind uncovers only for someone who stops.
//
// SEES. From two chunks out the snow drifts all lie the same way and grow commoner, and the wind is up: the
// blowing snow thickens toward one place, and from a chunk out spindrift streams off the drifts' crests (its
// FarTell, and the drift field's own streaks out to two chunks). Then the field itself: a place where the
// snow never stops blowing.
//
// DOES. Stillness, then touch. In the field the snow blows over everything. Stand still and the wind drops
// round him - the calm grows from his feet as he holds - and at StillNeeded seconds the snow has settled in
// a circle RevealRadius across: every rune-stone inside it shows under the snow, a dark cut with the warm
// glint on it ("a cut rune catching his light"). Walk away and the wind comes back, but a stone once shown
// stays shown (a mistake resets, never locks). Then touch all three, in any order: each one dug clear, its
// cut fills with Ice's light. The hold is two seconds, inside the five a secret may ask; it is not the
// thaw's verb (fight near the ice) and never a strike.
//
// PAYS. The third dug, the light leaves it and goes to him (SecretVault.Open): Sleet Runes the first time
// ever, granted and saved at once and offered from the next level-up; in a later run a chest from the secret
// budget. Nothing is solid: the stones lie flat in the snow and the horde walks over them.
//
// Art: tools/art/secret_frost.py (the stones, the drifts); the blowing snow and the calm are
// scenes/shaders/drift_field.gdshader on the world's 2-unit cell; the snow thrown off a dug stone and the
// light are stamped cells (VaultLight).
public partial class BuriedRunes : SecretVault
{
	/// <summary>The drift field: the snow blows hardest inside this, and stillness counts only here.</summary>
	public const float FieldRadius = 640f;

	/// <summary>The blowing snow thins out to nothing two chunks out (the far tell).</summary>
	public const float OuterRadius = 3072f;

	/// <summary>Seconds of standing still for the snow to settle (8.8: two).</summary>
	public const float StillNeeded = 2f;

	/// <summary>The settled circle: stones within this of his feet show (8.8: 400 px).</summary>
	public const float RevealRadius = 400f;

	/// <summary>His feet this near a shown stone dig it.</summary>
	public const float TouchRadius = 44f;

	public const int RuneCount = 3;

	// The wind: where the snow goes (west, a little south). The drifts in the art lie this way.
	private static readonly Vector2 Wind = new Vector2(-0.97f, 0.24f).Normalized();

	private const string ArtDir = "res://assets/bonelight/world/frost/";
	private const string ShaderPath = "res://scenes/shaders/drift_field.gdshader";
	// tools/art/secret_frost.py: DRIFT_MARGIN 3 art pixels under every drift, at x2 to the middle of that row.
	public const float DriftFoot = 5f;

	private static readonly SecretFarTell Tell = new()
	{
		Textures = new[] { ArtDir + "drift-1.png", ArtDir + "drift-2.png", ArtDir + "drift-3.png" },
		NearTextures = new[] { ArtDir + "drift-plume-1.png", ArtDir + "drift-plume-2.png" },
		InnerRadius = SecretCatalog.VaultFootprint,
		NearRadius = 1536f,
		OuterRadius = 3072f,
		PeakPerChunk = 46,
		NearShare = 0.6f,
		FootFromBottom = DriftFoot,
		// One wind made them all: every drift lies the way the art does, none flipped.
		LeanToward = false,
	};

	public override SecretFarTell FarTell => Tell;

	private readonly Vector2[] runeAt = new Vector2[RuneCount];
	private bool laidOut;

	/// <summary>
	/// Three stones in the field from the seed: each a seeded step from the middle (140 to 340 px), at least
	/// 200 px from the others, on open ground (no ice, water or wall), reserved so decor keeps off them.
	/// </summary>
	public override void LayOut(ExplorePlacer placer)
	{
		var placed = new List<Vector2>(RuneCount);
		for (int k = 0; k < RuneCount; k++)
			placed.Add(placer.Near(Position, 140f, 340f, 40f, placed, 200f, Position, 360f));
		for (int k = 0; k < RuneCount; k++)
			runeAt[k] = (placed[k] - Position).Round();
		laidOut = true;
	}

	private void EnsureLayout()
	{
		if (laidOut)
			return;
		for (int k = 0; k < RuneCount; k++)
			runeAt[k] = Vector2.FromAngle(k * Mathf.Tau / RuneCount + 0.4f) * 250f;
		laidOut = true;
	}

	/// <summary>A stone's point in the world. For the probe.</summary>
	public Vector2 RuneAt(int k) => GlobalPosition + runeAt[k];

	public bool IsShown(int k) => State != null && (State.Marks & (1UL << k)) != 0;

	public bool IsDug(int k) => State != null && (State.Marks & (1UL << (8 + k))) != 0;

	public int Shown => (IsShown(0) ? 1 : 0) + (IsShown(1) ? 1 : 0) + (IsShown(2) ? 1 : 0);

	public int Dug => State?.Step ?? 0;

	/// <summary>How far the snow has settled round him, 0..1 (1 at StillNeeded). For the probe.</summary>
	public float Settled { get; private set; }

	private readonly Sprite2D[] stones = new Sprite2D[RuneCount];
	private Node2D groundQuad;
	private Node2D airQuad;
	private ShaderMaterial groundMaterial;
	private ShaderMaterial airMaterial;
	private Node2D effects;
	private float clock;
	private Vector2 calmCentre;
	private float calmRadius;
	private float calmStrength;
	private bool posed;
	private readonly VaultLight light = new();
	private readonly List<(Vector2 At, float Born)> throws = new();

	private static readonly StringName ParamLayer = "layer";
	private static readonly StringName ParamCentre = "centre";
	private static readonly StringName ParamField = "field_radius";
	private static readonly StringName ParamOuter = "outer_radius";
	private static readonly StringName ParamTime = "time";
	private static readonly StringName ParamWind = "wind";
	private static readonly StringName ParamCalmCentre = "calm_centre";
	private static readonly StringName ParamCalmRadius = "calm_radius";
	private static readonly StringName ParamCalmStrength = "calm_strength";

	public override void _Ready()
	{
		EnsureLayout();
		Shader shader = ResourceLoader.Exists(ShaderPath) ? GD.Load<Shader>(ShaderPath) : null;
		if (shader == null)
			GD.PushError($"BuriedRunes: {ShaderPath} did not load.");
		groundMaterial = MakeMaterial(shader, 0);
		airMaterial = MakeMaterial(shader, 1);
		// The calm lies on the ground under the bodies; the blowing snow goes over them.
		groundQuad = new Node2D { Name = "Calm", ZIndex = DrawLayers.Ground, ZAsRelative = false, Material = groundMaterial };
		groundQuad.Draw += DrawGroundQuad;
		AddChild(groundQuad);
		airQuad = new Node2D { Name = "Snow", ZIndex = DrawLayers.Air, ZAsRelative = false, Material = airMaterial };
		airQuad.Draw += DrawAirQuad;
		AddChild(airQuad);

		string path = ArtDir + "rune-stone.png";
		Texture2D sheet = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		if (sheet == null)
			GD.PushError($"BuriedRunes: {path} is missing; re-run python tools/art/secret_frost.py, then import.");
		for (int k = 0; k < RuneCount; k++)
		{
			// Flat in the snow, under the bodies (the drift field's stones are walked over).
			stones[k] = new Sprite2D
			{
				Name = $"Rune{k}",
				Texture = sheet,
				Hframes = 6,
				Position = runeAt[k],
				ZIndex = DrawLayers.Decal,
				ZAsRelative = false,
				Visible = false,
			};
			AddChild(stones[k]);
		}

		effects = new Node2D { Name = "Light", ZIndex = DrawLayers.Air, ZAsRelative = false, TopLevel = true };
		effects.Draw += DrawEffects;
		AddChild(effects);
		effects.GlobalPosition = Vector2.Zero;

		foreach (ShaderMaterial m in new[] { groundMaterial, airMaterial })
		{
			m.SetShaderParameter(ParamCentre, GlobalPosition);
			m.SetShaderParameter(ParamField, FieldRadius);
			m.SetShaderParameter(ParamOuter, OuterRadius);
			m.SetShaderParameter(ParamWind, Wind);
		}
		ShowStones();
	}

	private static ShaderMaterial MakeMaterial(Shader shader, int layer)
	{
		var material = new ShaderMaterial { Shader = shader };
		material.SetShaderParameter(ParamLayer, layer);
		return material;
	}

	public override void _ExitTree()
	{
		if (groundQuad != null && IsInstanceValid(groundQuad))
			groundQuad.Draw -= DrawGroundQuad;
		if (airQuad != null && IsInstanceValid(airQuad))
			airQuad.Draw -= DrawAirQuad;
		if (effects != null && IsInstanceValid(effects))
			effects.Draw -= DrawEffects;
		base._ExitTree();
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		clock += dt;
		for (int i = throws.Count - 1; i >= 0; i--)
			if (clock - throws[i].Born > 0.6f)
				throws.RemoveAt(i);
		if (light.Tick(dt))
			// Rewards in flight are paid even if the boss has come meanwhile (stage-secrets section 4).
			Open(GlobalPosition + new Vector2(0f, 60f));

		Player player = Player;
		if (player == null)
			return;
		bool near = player.GlobalPosition.DistanceSquaredTo(GlobalPosition) < (OuterRadius + 900f) * (OuterRadius + 900f);
		if (!posed)
			Settle(player, dt);
		if (Active && !IsOpened)
			Dig(player);
		ShowStones();
		if (near)
		{
			airMaterial.SetShaderParameter(ParamTime, clock);
			foreach (ShaderMaterial m in new[] { groundMaterial, airMaterial })
			{
				m.SetShaderParameter(ParamCalmCentre, calmCentre);
				m.SetShaderParameter(ParamCalmRadius, calmRadius);
				m.SetShaderParameter(ParamCalmStrength, calmStrength);
			}
			if (player.GlobalPosition.DistanceSquaredTo(GlobalPosition) < 1600f * 1600f)
				effects.QueueRedraw();
		}
	}

	// Standing still in the field: the wind drops round his feet, the calm growing as he holds; at
	// StillNeeded it has settled RevealRadius across and the stones in it show. Moving on, it dissolves.
	private void Settle(Player player, float dt)
	{
		Vector2 feet = player.FootPosition;
		bool inField = feet.DistanceTo(GlobalPosition) < FieldRadius;
		if (Active && inField && player.IsStandingStill && !player.IsDead)
		{
			Settled = Mathf.Clamp((player.StillSeconds - 0.3f) / (StillNeeded - 0.3f), 0f, 1f);
			if (Settled > 0f)
			{
				calmCentre = feet;
				calmRadius = RevealRadius * (1f - (1f - Settled) * (1f - Settled));
				calmStrength = 1f;
			}
			if (player.StillSeconds >= StillNeeded)
				RevealAround(feet);
		}
		else
		{
			Settled = 0f;
			calmStrength = Mathf.Max(0f, calmStrength - dt / 2.5f);
		}
	}

	private void RevealAround(Vector2 feet)
	{
		SecretDirector.SiteState state = State;
		if (state == null)
			return;
		for (int k = 0; k < RuneCount; k++)
		{
			if (IsShown(k) || RuneAt(k).DistanceTo(feet) > RevealRadius)
				continue;
			state.Marks |= 1UL << k;
			SfxPlayer.AtPosition(SfxCatalog.SpellSleetRune, RuneAt(k), 0.05f);
		}
	}

	// A shown stone he walks onto is dug clear: the snow thrown off, its cut lit. Three, and the light goes.
	private void Dig(Player player)
	{
		SecretDirector.SiteState state = State;
		if (state == null || state.Step >= RuneCount)
			return;
		Vector2 feet = player.FootPosition;
		for (int k = 0; k < RuneCount; k++)
		{
			if (!IsShown(k) || IsDug(k) || feet.DistanceSquaredTo(RuneAt(k)) > TouchRadius * TouchRadius)
				continue;
			DigNow(k);
		}
	}

	private void DigNow(int k)
	{
		SecretDirector.SiteState state = State;
		state.Marks |= 1UL << k | 1UL << (8 + k);
		state.Step++;
		throws.Add((RuneAt(k), clock));
		SfxPlayer.Impact(Element.Ice, RuneAt(k));
		if (state.Step >= RuneCount)
			light.Release(RuneAt(k));
	}

	private void ShowStones()
	{
		for (int k = 0; k < RuneCount; k++)
		{
			if (stones[k] == null)
				continue;
			stones[k].Visible = IsShown(k);
			stones[k].Frame = 2 * k + (IsDug(k) ? 1 : 0);
		}
	}

	/// <summary>
	/// For _UiShot's photographs (--secret-stage=N), never in play: 1 the snow settling round the middle, 2
	/// settled - the three shown, 3 two dug, 4 the third dug - the light, and the pay.
	/// </summary>
	public void PoseForProbe(int stage)
	{
		SecretDirector.SiteState state = State;
		if (state == null)
			return;
		posed = stage < 4;
		calmCentre = GlobalPosition;
		calmStrength = 1f;
		calmRadius = stage <= 1 ? RevealRadius * 0.55f : RevealRadius;
		if (stage >= 2)
			for (int k = 0; k < RuneCount; k++)
				state.Marks |= 1UL << k;
		if (stage >= 3)
			for (int k = 0; k < 2; k++)
				if (!IsDug(k))
					DigNow(k);
		if (stage >= 4 && !IsDug(2))
			DigNow(2);
	}

	// --- drawing ------------------------------------------------------------------------------------

	private void DrawGroundQuad()
	{
		// The calm never reaches past the field and its reveal circle.
		float r = FieldRadius + RevealRadius;
		groundQuad.DrawRect(new Rect2(-r, -r, 2f * r, 2f * r), Colors.White);
	}

	private void DrawAirQuad()
	{
		airQuad.DrawRect(new Rect2(-OuterRadius, -OuterRadius, 2f * OuterRadius, 2f * OuterRadius), Colors.White);
	}

	private static readonly Color SnowHi = new(0.933f, 0.949f, 0.980f);    // bone hi #EEF2FA
	private static readonly Color SnowBase = new(0.604f, 0.643f, 0.722f);  // bone base #9AA4B8

	private void DrawEffects()
	{
		// The near tell on every stone shown and not yet dug: a cut rune catching his light.
		for (int k = 0; k < RuneCount; k++)
			if (IsShown(k) && !IsDug(k))
				VaultLight.DrawGlint(effects, RuneAt(k) + new Vector2(0f, -2f), clock + k * 0.8f);
		foreach (var (at, born) in throws)
			VaultLight.DrawThrow(effects, at, (clock - born) / 0.6f, 12, 34f, SnowHi, SnowBase, born);
		if (light.InFlight && Player is Player player)
			light.Draw(effects, player.GlobalPosition + new Vector2(0f, -40f));
	}
}
