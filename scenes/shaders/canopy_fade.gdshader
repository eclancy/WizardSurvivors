// A tree's crown thins round the wizard when he walks behind it (.ai/terrain.md section 10).
//
// One material shared by every tree on the stage, so the cost is one uniform write a frame
// (Node2DGame.UpdateCanopyFade) and nothing per tree: no Area2D, no per-tree script, no query.
// The tree's node is its sort key - WorldSprites.SortLift above its ground line - so the fade
// starts exactly when y-sort starts drawing the tree over the wizard, and never when he is in
// front of it.
//
// The radius is wider than the wizard on purpose. A canopy hides what stands under it, and the
// enemies that matter are the ones about to reach him: the hole is big enough to show the ring
// of the horde closest to him as well as his own figure. Enemies under a canopy far from him stay
// hidden; that is a readability cost of big trees, and what keeps it small is density - see
// Node2DGame.BuildDecorProps.
//
// Dithered on the art's own 2 px grid rather than blended: a half-transparent crown would invent
// colours the palette does not have (.ai/art-direction.md section 3), and an ordered screen of
// whole art pixels reads as the leaves thinning.
shader_type canvas_item;

uniform vec2 player_pos = vec2(1e9, 1e9);
uniform float radius = 150.0;
uniform float keep = 0.3;

varying vec2 world_pos;
varying float sort_y;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	sort_y = (MODEL_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).y;
}

float bayer4(vec2 p) {
	int x = int(mod(p.x, 4.0));
	int y = int(mod(p.y, 4.0));
	int i = x + y * 4;
	// 4x4 ordered thresholds, 0..15
	int m[16] = int[16](0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5);
	return (float(m[i]) + 0.5) / 16.0;
}

void fragment() {
	// COLOR arrives as texture(TEXTURE, UV) times the modulate, so it is the tree's pixel already.
	// This used to sample the texture again and multiply the two, which squared every colour: every
	// tree in the game drew darker than its art (found on the Broken Circle's standing stones).
	// Behind: drawn under the tree by y-sort. A few pixels of ramp so the hole opens rather than pops.
	float behind = clamp((sort_y - player_pos.y) / 12.0, 0.0, 1.0);
	// Round his body, a little squashed: the view is three-quarter, not top-down.
	vec2 d = (world_pos - player_pos) * vec2(1.0, 1.25);
	float inside = 1.0 - smoothstep(radius * 0.55, radius, length(d));
	float thin = behind * inside * (1.0 - keep);
	// Whole art pixels (x2), so the screen never splits a pixel of the drawing.
	vec2 art = floor(world_pos * 0.5);
	if (thin > bayer4(art)) {
		COLOR.a = 0.0;
	}
}
