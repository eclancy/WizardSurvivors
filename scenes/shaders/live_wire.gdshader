// Live Wire (scripts/LiveWireLook.cs, Gale Blade's level 4): a crackling wire of lightning from the wizard
// to his blade while it is out - as pixel art on the game's 2 px grid.
//
// It was three stroked polylines from his hand to the blade - a 5 px translucent band of Lightning's edge
// under a hot line and a white one - at whatever angle the blade was: the smooth vector look the
// Bonelight contract rules out. Now the wire is one quad laid along it, and every fragment snaps itself
// to the WORLD's 2 px cell first and only then measures that cell against the jagged line, as
// stormsweep_lash.gdshader does, so the bolt is a staircase of square art pixels at any angle. A
// white-hot core one art pixel wide, a hot pixel either side of it where it kinks, Lightning's saturated
// edge as a fringe outside that (it holds the wire on a pale floor), and the odd spark of Lightning's mid
// and edge thrown off it; re-jagged every few frames so it crackles rather than hangs.
// Every colour is an opaque Lightning tone; nothing is blended.
//
// THE WIRE IS THE CUT. The quad runs from the point the cut measures from (the wizard's position) to
// the blade, `wire_length` long, and `half_width` is BoltEvolutions.LiveWireHalfWidth - the band either side
// of that segment whose enemies it cuts (with a body's allowance on top, as every zone). The kinks and
// the sparks stay inside that band, so the wire is never drawn anywhere it does not cut.
shader_type canvas_item;
render_mode unshaded;

uniform float wire_length = 100.0;
uniform float half_width = 6.0;
uniform float seed = 0.0;
uniform bool gilded = false;

const vec3 LIT_CORE = vec3(1.0, 1.0, 0.941);         // lightning core #FFFFF0
const vec3 LIT_HOT = vec3(1.0, 0.973, 0.690);        // lightning hot #FFF8B0
const vec3 LIT_MID = vec3(1.0, 0.941, 0.416);        // lightning mid #FFF06A
const vec3 LIT_EDGE = vec3(0.878, 0.690, 0.165);     // lightning edge #E0B02A
const vec3 GOLD_HI = vec3(1.0, 0.941, 0.753);        // gold hi #FFF0C0
const float GRID = 2.0;
// How often the wire is re-jagged, in seconds (ElementalBolt's old 0.06).
const float JAG_SECONDS = 0.06;
const float BODY_GAP = 12.0;

varying vec2 v_world;
varying flat vec2 v_origin;
varying flat vec2 v_axis_x;
varying flat vec2 v_axis_y;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	v_origin = MODEL_MATRIX[3].xy;
	v_axis_x = normalize(MODEL_MATRIX[0].xy);
	v_axis_y = normalize(MODEL_MATRIX[1].xy);
}

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031 + seed * 0.0137);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float seg_dist(vec2 p, vec2 a, vec2 b) {
	vec2 ab = b - a;
	float t = clamp(dot(p - a, ab) / max(dot(ab, ab), 0.0001), 0.0, 1.0);
	return length(p - (a + ab * t));
}

vec4 shade_cell(vec2 world, vec2 origin, vec2 axis_x, vec2 axis_y) {
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	vec2 rel = centre - origin;
	// In the wire's frame, in world px: x from the wizard to the blade, y across.
	vec2 p = vec2(dot(rel, axis_x), dot(rel, axis_y));
	// It leaves him at the edge of his body, not from under his robe: the first few px of the segment are
	// inside his sprite, and drawing them would lay the wire across him.
	if (p.x < BODY_GAP || p.x > wire_length + GRID || abs(p.y) > half_width)
		return vec4(0.0);

	float jag = floor(TIME / JAG_SECONDS);
	// One kink every couple of dozen px, the ends pinned to the wizard and the blade.
	float kinks = max(3.0, floor(wire_length / 22.0));
	float swing = max(0.0, half_width - GRID * 1.5);
	float k = clamp(floor(p.x / wire_length * kinks), 0.0, kinks - 1.0);
	float d = 1e6;
	// The segment this cell is over and its neighbours, for the corners.
	for (int s = -1; s <= 1; s++) {
		float i = k + float(s);
		if (i < 0.0 || i > kinks - 1.0)
			continue;
		float ya = (i <= 0.0) ? 0.0 : (hash12(vec2(i, jag)) * 2.0 - 1.0) * swing;
		float yb = (i + 1.0 >= kinks) ? 0.0 : (hash12(vec2(i + 1.0, jag)) * 2.0 - 1.0) * swing;
		vec2 a = vec2(wire_length * i / kinks, ya);
		vec2 b = vec2(wire_length * (i + 1.0) / kinks, yb);
		d = min(d, seg_dist(p, a, b));
	}

	// The wire's normal is close to the frame's y; one art pixel thick across the grid at this angle.
	float thin = 0.5 * GRID * (abs(axis_y.x) + abs(axis_y.y));
	if (d <= thin) {
		return vec4(gilded && hash12(cell + jag) > 0.8 ? GOLD_HI : LIT_CORE, 1.0);
	}
	if (d <= thin + GRID * 0.75 && hash12(cell + jag * 1.7) > 0.25) {
		return vec4(LIT_HOT, 1.0);
	}
	// Lightning's saturated edge either side: the fringe that holds the wire on a pale floor (on the
	// Sketchbook's paper a bare white and pale-yellow line all but vanished), a gold glow on a dark one.
	if (d <= thin + GRID * 1.6) {
		return vec4(LIT_EDGE, 1.0);
	}
	// Sparks thrown off it, inside the band it cuts.
	float spark = hash12(cell * 1.3 + jag * 3.1);
	if (d <= thin + GRID * 2.5 && spark > 0.94) {
		return vec4(spark > 0.975 ? LIT_MID : LIT_EDGE, 1.0);
	}
	return vec4(0.0);
}

void fragment() {
	// Godot allows no return in fragment(): the cell is shaded by shade_cell, and alpha 0 is nothing.
	vec4 c = shade_cell(v_world, v_origin, v_axis_x, v_axis_y);
	if (c.a < 0.5)
		discard;
	COLOR = c;
}
