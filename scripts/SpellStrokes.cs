using Godot;
using System;
using System.Runtime.InteropServices;

namespace WizardSurvivors.scripts;

/// <summary>
/// Straight strokes drawn as pixel art on the world's 2 px grid (scenes/shaders/spell_strokes.gdshader):
/// the lines of the trap and star spells, which were DrawLine and DrawPolyline - smooth, rotated quads,
/// translucent besides. Not a node: an owner keeps one, fills it from its own _Draw and draws it into a
/// canvas whose <c>Material</c> is <see cref="Material"/>.
/// </summary>
/// <remarks>
/// <para><b>Use.</b> In the canvas's draw: <see cref="Begin"/>, one <see cref="Add"/> per stroke (A and B
/// in WORLD px, from the numbers the owner's hit test reads), then <see cref="Draw"/>. The owner sets its
/// six tones once with <see cref="SetTones"/> - an element ramp's core, hot, mid and edge and two darks -
/// and every stroke names its spine, body and glow by tone.</para>
///
/// <para><b>Nothing allocated after construction.</b> The strokes go to the GPU as a float texture, four
/// texels a row (stormsweep_lash's way: a Variant of a packed array per frame allocates a finalizable
/// disposer on the C# side), and each is one quad drawn with DrawPrimitive from reused arrays.</para>
/// </remarks>
public sealed class SpellStrokes
{
	public const string ShaderPath = "res://scenes/shaders/spell_strokes.gdshader";
	private const int Texels = 4;
	private const float Grid = 2f;

	/// <summary>One stroke. Zero is every field's off: no glow, no tail, no dissolve, no flash.</summary>
	public struct Stroke
	{
		public Vector2 A;
		public Vector2 B;
		/// <summary>Half the body's width at A and at B (a trail tapers).</summary>
		public float HalfA;
		public float HalfB;
		/// <summary>How far past the body the dithered glow reaches, and its density 0..1.</summary>
		public float Glow;
		public float Density;
		/// <summary>The tones of the spine, the body and the glow (0..5).</summary>
		public int Spine;
		public int Body;
		public int GlowTone;
		/// <summary>Step the tones one stop down at 30% and 65% of the way to B.</summary>
		public bool Cool;
		/// <summary>Light the key side of the body and shade the far side.</summary>
		public bool Lit;
		/// <summary>The glow alone: a dithered ring from HalfA out to HalfA + Glow, nothing inside it.</summary>
		public bool Hollow;
		/// <summary>The share of the stroke, from B, that dissolves toward B.</summary>
		public float Tail;
		/// <summary>How much of the whole stroke has dissolved, 0..1.</summary>
		public float Dissolve;
		/// <summary>True: the spine white-hot (tone 0), the body a stop up.</summary>
		public bool Flash;
		/// <summary>The spine's half width in px; 0 is one art pixel's line.</summary>
		public float SpineHalf;
	}

	private static Shader shader;
	private static readonly StringName ParamData = "stroke_data";
	private static readonly StringName[] ParamTones = { "tone0", "tone1", "tone2", "tone3", "tone4", "tone5" };

	private readonly int max;
	private readonly byte[] bytes;
	private readonly Image image;
	private readonly ImageTexture texture;
	private readonly Vector2[] quads;
	private readonly Vector2[] quad = new Vector2[4];
	private readonly Vector2[] quadUv = new Vector2[4];
	private readonly Color[] quadColours = new Color[4];
	private int count;

	/// <summary>The material the canvas the strokes are drawn into must carry.</summary>
	public ShaderMaterial Material { get; }

	public int Count => count;

	public SpellStrokes(int maxStrokes)
	{
		max = Math.Max(1, maxStrokes);
		bytes = new byte[Texels * max * 16];
		quads = new Vector2[max * 4];
		shader ??= GD.Load<Shader>(ShaderPath);
		if (shader == null)
			GD.PushError($"SpellStrokes: {ShaderPath} failed to load.");
		image = Image.CreateEmpty(Texels, max, false, Image.Format.Rgbaf);
		texture = ImageTexture.CreateFromImage(image);
		Material = new ShaderMaterial { Shader = shader };
		Material.SetShaderParameter(ParamData, texture);
	}

	/// <summary>The six tones, brightest first: core, hot, mid, edge, then two darks.</summary>
	public void SetTones(Color t0, Color t1, Color t2, Color t3, Color t4, Color t5)
	{
		Material.SetShaderParameter(ParamTones[0], t0);
		Material.SetShaderParameter(ParamTones[1], t1);
		Material.SetShaderParameter(ParamTones[2], t2);
		Material.SetShaderParameter(ParamTones[3], t3);
		Material.SetShaderParameter(ParamTones[4], t4);
		Material.SetShaderParameter(ParamTones[5], t5);
	}

	/// <summary>An element ramp as tones 0-3, and two darks after it.</summary>
	public void SetTones(in ElementColors.Ramp ramp, Color dark, Color occ) =>
		SetTones(ramp.Core, ramp.Hot, ramp.Mid, ramp.Edge, dark, occ);

	public void Begin() => count = 0;

	/// <summary>Adds a stroke; false when the buffer is full or the stroke has no length and no width.</summary>
	public bool Add(in Stroke s)
	{
		if (count >= max)
			return false;
		float reach = MathF.Max(s.HalfA, s.HalfB) + s.Glow + Grid * 2f;
		Vector2 ab = s.B - s.A;
		float length = ab.Length();
		Vector2 dir = length > 0.01f ? ab / length : Vector2.Right;
		Vector2 n = new Vector2(-dir.Y, dir.X) * reach;
		Vector2 back = s.A - dir * reach;
		Vector2 front = s.B + dir * reach;
		int q = count * 4;
		quads[q] = back + n;
		quads[q + 1] = front + n;
		quads[q + 2] = front - n;
		quads[q + 3] = back - n;

		int flags = (s.Cool ? 1 : 0) | (s.Lit ? 2 : 0) | (s.Hollow ? 4 : 0);
		Span<Vector4> texels = MemoryMarshal.Cast<byte, Vector4>(bytes.AsSpan());
		int r = count * Texels;
		texels[r] = new Vector4(s.A.X, s.A.Y, s.B.X, s.B.Y);
		texels[r + 1] = new Vector4(s.HalfA, s.HalfB, s.Glow, s.Density);
		texels[r + 2] = new Vector4(s.Spine, s.Body, s.GlowTone, flags);
		texels[r + 3] = new Vector4(s.Tail, s.Dissolve, s.Flash ? 1f : 0f, s.SpineHalf);
		count++;
		return true;
	}

	/// <summary>
	/// Uploads the strokes and draws one quad each into <paramref name="canvas"/>, whose Material must be
	/// <see cref="Material"/>. Call from the canvas's own draw. <paramref name="tint"/> multiplies every tone.
	/// </summary>
	public void Draw(CanvasItem canvas, Color tint)
	{
		if (count == 0)
			return;
		image.SetData(Texels, max, false, Image.Format.Rgbaf, bytes);
		texture.Update(image);
		Transform2D toLocal = canvas.GetGlobalTransform().AffineInverse();
		for (int k = 0; k < 4; k++)
			quadColours[k] = tint;
		for (int i = 0; i < count; i++)
		{
			for (int k = 0; k < 4; k++)
			{
				quad[k] = toLocal * quads[i * 4 + k];
				quadUv[k] = new Vector2(i + 0.5f, 0f);
			}
			canvas.DrawPrimitive(quad, quadColours, quadUv);
		}
	}
}
