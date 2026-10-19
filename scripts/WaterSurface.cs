using System;
using System.Collections.Generic;
using Godot;
using WizardSurvivors.scripts;

/// <summary>
/// One chunk's open water: a distance field drawn by scenes/shaders/water_surface.gdshader, and
/// the layer-4 collision body the feet and the enemies stop against - both from the same
/// WaterShape polygons. One node a chunk, freed with it. See .ai/terrain.md section 3.
/// </summary>
/// <remarks>
/// THE FIELD. A 6 px grid of signed distances to the shore (positive in the water), clamped to
/// +-12 px (all the bands need) and stored as one byte a texel, for the chunk and one cell round it. The shader reads it
/// with bilinear filtering at the middle of every world pixel, so the shore is a smooth curve drawn
/// on the art's own pixel grid - no stairs, no anti-aliased smear - and the bands either side of it
/// (the mud lip, the dark water under the bank) come from the same number. Only cells near water are
/// computed; the rest is "far from any water" and costs one byte store. Built in two streamer
/// steps (TerrainStreamer): the shape and the field, then the texture and the body - about 1 ms and
/// under 1 ms a chunk on the Swamp in a debug build.
///
/// THE BODY. A server-side static body (PhysicsServer2D, no nodes): one convex shape per partial
/// cell and one rectangle per run of whole-water cells, on LevelTilePainter.TerrainCollisionLayer.
/// It used to be a polygon on each water TILE's source; a tile can only carry one shape for every
/// cell that uses it, and a smooth shore is different in every cell.
/// </remarks>
public partial class WaterSurface : Node2D
{
	public const int TexelPx = 6;
	public const float Range = 12f;
	private const int TexelsPerCell = WaterShape.CellPx / TexelPx;

	private Texture2D field;
	private Rect2 dest, src;
	private Rid body;
	private readonly List<Rid> shapes = new();

	public override void _Draw()
	{
		if (field != null)
			DrawTextureRectRegion(field, dest, src);
	}

	public override void _ExitTree()
	{
		if (body.IsValid)
			PhysicsServer2D.FreeRid(body);
		foreach (Rid s in shapes)
			PhysicsServer2D.FreeRid(s);
		body = default;
		shapes.Clear();
	}

	/// <summary>
	/// Builds and paints one chunk's water from a WaterShape already built for it, and adds itself
	/// under <paramref name="parent"/>. Returns null when the chunk has no water near it.
	/// </summary>
	public sealed class Builder
	{
		private readonly int chunk;
		private readonly int tex;
		private readonly byte[] texels;
		private readonly float[] dist2;
		private readonly List<Vector2> segs = new();

		public Builder(int chunkTiles)
		{
			chunk = chunkTiles;
			tex = (chunkTiles + 2) * TexelsPerCell;
			texels = new byte[tex * tex];
			dist2 = new float[tex * tex];
		}

		private Rect2I drawCells;

		/// <summary>
		/// The first half of a chunk's water: the distance field, from a shape already built for the
		/// chunk. False when nothing in the chunk is near water, and there is no second half.
		/// </summary>
		public bool Prepare(WaterShape shape, Vector2I minCell)
		{
			// Cells near water inside the chunk, for the draw rectangle; nothing near, nothing to do.
			int bx0 = int.MaxValue, by0 = int.MaxValue, bx1 = int.MinValue, by1 = int.MinValue;
			for (int y = 0; y < chunk; y++)
			{
				for (int x = 0; x < chunk; x++)
				{
					if (!NearWater(shape, minCell.X + x, minCell.Y + y)) continue;
					bx0 = Math.Min(bx0, x); by0 = Math.Min(by0, y);
					bx1 = Math.Max(bx1, x); by1 = Math.Max(by1, y);
				}
			}
			if (bx0 == int.MaxValue)
				return false;
			drawCells = new Rect2I(bx0, by0, bx1 - bx0 + 1, by1 - by0 + 1);
			FillField(shape, minCell);
			return true;
		}

		/// <summary>The second half: the texture, the node and the collision body. Adds the node under
		/// <paramref name="parent"/>.</summary>
		public WaterSurface Finish(WaterShape shape, Vector2I minCell, Node2D parent, Material material,
			Transform2D painterToWorld, Rid space, uint collisionLayer)
		{
			var image = Image.CreateFromData(tex, tex, false, Image.Format.R8, texels);
			var node = new WaterSurface
			{
				Name = "Water",
				field = ImageTexture.CreateFromImage(image),
				Material = material,
				TextureFilter = TextureFilterEnum.Linear,
				// The field starts one cell before the chunk, for the margin texels the filter reads.
				Position = new Vector2(minCell.X - 1, minCell.Y - 1) * WaterShape.CellPx,
			};
			// Draw only the chunk's own cells that have water near them: the neighbour draws its own,
			// and every pixel of the quad runs the shader whether or not it is water.
			var r = new Rect2((drawCells.Position.X + 1) * WaterShape.CellPx, (drawCells.Position.Y + 1) * WaterShape.CellPx,
				drawCells.Size.X * WaterShape.CellPx, drawCells.Size.Y * WaterShape.CellPx);
			node.dest = r;
			node.src = new Rect2(r.Position / TexelPx, r.Size / TexelPx);
			parent.AddChild(node);
			BuildBody(node, shape, minCell, painterToWorld, space, collisionLayer);
			return node;
		}

		private static bool NearWater(WaterShape shape, int cx, int cy)
		{
			for (int dy = -1; dy <= 1; dy++)
				for (int dx = -1; dx <= 1; dx++)
					if (shape.KindAt(cx + dx, cy + dy) > 0)
						return true;
			return false;
		}

		// Distances by SPLATTING: each shoreline segment updates only the texels within Range of it,
		// then each texel near water takes its sign from its cell's polygon. Per texel over every
		// nearby segment was 7-15 ms a chunk in a debug build; this is a few hundred texels a segment.
		private void FillField(WaterShape shape, Vector2I minCell)
		{
			Array.Clear(texels);
			Array.Fill(dist2, Range * Range);
			// The field's texel (0, 0) is the corner of cell (minCell - 1).
			float fx = (minCell.X - 1) * WaterShape.CellPx, fy = (minCell.Y - 1) * WaterShape.CellPx;
			for (int y = -WaterShape.ShapeRing; y < chunk + WaterShape.ShapeRing; y++)
			{
				for (int x = -WaterShape.ShapeRing; x < chunk + WaterShape.ShapeRing; x++)
				{
					if (shape.KindAt(minCell.X + x, minCell.Y + y) != 2) continue;
					segs.Clear();
					shape.ShoreSegments(minCell.X + x, minCell.Y + y, segs);
					for (int i = 0; i + 1 < segs.Count; i += 2)
						Splat(segs[i], segs[i + 1], fx, fy);
				}
			}

			for (int y = -1; y <= chunk; y++)
			{
				for (int x = -1; x <= chunk; x++)
				{
					int cx = minCell.X + x, cy = minCell.Y + y;
					if (!NearWater(shape, cx, cy)) continue;
					int kind = shape.KindAt(cx, cy);
					ReadOnlySpan<Vector2> poly = shape.PolygonAt(cx, cy);
					float ox = cx * WaterShape.CellPx, oy = cy * WaterShape.CellPx;
					int tx0 = (x + 1) * TexelsPerCell, ty0 = (y + 1) * TexelsPerCell;
					const float far = Range * Range;
					for (int ty = 0; ty < TexelsPerCell; ty++)
					{
						int row = (ty0 + ty) * tex + tx0;
						float py = oy + (ty + 0.5f) * TexelPx;
						for (int txi = 0; txi < TexelsPerCell; txi++)
						{
							float d2 = dist2[row + txi];
							bool inside = kind == 1 || (kind == 2 && InsideConvex(poly, ox + (txi + 0.5f) * TexelPx, py));
							if (d2 >= far)
							{
								texels[row + txi] = inside ? (byte)255 : (byte)0;
								continue;
							}
							float d = Mathf.Sqrt(d2);
							float signed = inside ? d : -d;
							texels[row + txi] = (byte)Mathf.Clamp((int)((signed + Range) / (2f * Range) * 255f + 0.5f), 0, 255);
						}
					}
				}
			}
		}

		// Point in a WaterShape polygon (convex, clockwise on screen), in floats: the Vector2 operators
		// are not inlined in a debug build, and this runs for every texel near water.
		private static bool InsideConvex(ReadOnlySpan<Vector2> poly, float px, float py)
		{
			int n = poly.Length;
			for (int i = 0; i < n; i++)
			{
				Vector2 a = poly[i], b = poly[i + 1 == n ? 0 : i + 1];
				if ((b.X - a.X) * (py - a.Y) - (b.Y - a.Y) * (px - a.X) < -0.001f)
					return false;
			}
			return true;
		}

		private void Splat(Vector2 a, Vector2 b, float fx, float fy)
		{
			int x0 = Math.Max(0, (int)((Mathf.Min(a.X, b.X) - Range - fx) / TexelPx));
			int x1 = Math.Min(tex - 1, (int)((Mathf.Max(a.X, b.X) + Range - fx) / TexelPx));
			int y0 = Math.Max(0, (int)((Mathf.Min(a.Y, b.Y) - Range - fy) / TexelPx));
			int y1 = Math.Min(tex - 1, (int)((Mathf.Max(a.Y, b.Y) + Range - fy) / TexelPx));
			float abx = b.X - a.X, aby = b.Y - a.Y;
			float len2 = abx * abx + aby * aby;
			float inv = len2 > 1e-6f ? 1f / len2 : 0f;
			for (int ty = y0; ty <= y1; ty++)
			{
				float py = fy + (ty + 0.5f) * TexelPx - a.Y;
				int row = ty * tex;
				for (int tx = x0; tx <= x1; tx++)
				{
					float px = fx + (tx + 0.5f) * TexelPx - a.X;
					float t = (px * abx + py * aby) * inv;
					t = t < 0f ? 0f : (t > 1f ? 1f : t);
					float dx = px - abx * t, dy = py - aby * t;
					float d2 = dx * dx + dy * dy;
					if (d2 < dist2[row + tx]) dist2[row + tx] = d2;
				}
			}
		}

		private void BuildBody(WaterSurface node, WaterShape shape, Vector2I minCell, Transform2D xf, Rid space, uint layer)
		{
			Rid b = PhysicsServer2D.BodyCreate();
			PhysicsServer2D.BodySetMode(b, PhysicsServer2D.BodyMode.Static);
			PhysicsServer2D.BodySetSpace(b, space);
			PhysicsServer2D.BodySetCollisionLayer(b, layer);
			PhysicsServer2D.BodySetCollisionMask(b, 0);
			PhysicsServer2D.BodyAttachObjectInstanceId(b, node.GetInstanceId());
			PhysicsServer2D.BodySetState(b, PhysicsServer2D.BodyState.Transform, xf);
			node.body = b;

			for (int y = 0; y < chunk; y++)
			{
				int run = -1;
				for (int x = 0; x <= chunk; x++)
				{
					int k = x < chunk ? shape.KindAt(minCell.X + x, minCell.Y + y) : 0;
					if (k == 1)
					{
						if (run < 0) run = x;
						continue;
					}
					if (run >= 0)
					{
						// A run of whole-water cells: one rectangle.
						Rid rect = PhysicsServer2D.RectangleShapeCreate();
						float w = (x - run) * WaterShape.CellPx;
						PhysicsServer2D.ShapeSetData(rect, new Vector2(w * 0.5f, WaterShape.CellPx * 0.5f));
						var centre = new Vector2((minCell.X + run) * WaterShape.CellPx + w * 0.5f,
							(minCell.Y + y) * WaterShape.CellPx + WaterShape.CellPx * 0.5f);
						PhysicsServer2D.BodyAddShape(b, rect, new Transform2D(0f, centre));
						node.shapes.Add(rect);
						run = -1;
					}
					if (k == 2)
					{
						ReadOnlySpan<Vector2> poly = shape.PolygonAt(minCell.X + x, minCell.Y + y);
						Rid convex = PhysicsServer2D.ConvexPolygonShapeCreate();
						PhysicsServer2D.ShapeSetData(convex, poly.ToArray());
						PhysicsServer2D.BodyAddShape(b, convex, Transform2D.Identity);
						node.shapes.Add(convex);
					}
				}
			}
		}
	}
}
