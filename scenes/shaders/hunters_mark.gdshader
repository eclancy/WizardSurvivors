// Hunter's Mark (scripts/HuntersMark.cs, Hunter's Draw's level 8): four arrowheads pointing in at the
// quarry a full draw marked, turning slowly round it - as pixel art on the game's 2 px grid.
//
// It was eight rotated polygons, an outline set under the ink at 90% opacity: at every angle but the four
// square ones their edges were smooth and their pixels on a rotated grid. Now the mark is one quad on the
// quarry and every fragment snaps itself to the WORLD's 2 px cell first and is then measured against the
// four heads, so they stay square-pixelled and upright as they turn. Each head is the arrow's own - the
// Metal-white point in toward the quarry, Wind's hot and mid on its flanks (the lit one toward the key from
// the upper left) - backed by an art pixel of occlusion so it holds on any floor and over any body. As the
// mark runs out it dissolves through the Bayer matrix instead of fading. Every colour is opaque.
//
// A MARK, NOT AN AREA (.ai/art-direction.md section 3): it is drawn round one body, the one that takes
// more, at a fixed size - nothing here measures a reach.
shader_type canvas_item;
render_mode unshaded;

uniform float spin = 0.0;
// Where the heads' points stand from the quarry, px; the bases are HEAD_LEN further out.
uniform float ring = 30.0;
// 1 whole .. 0 gone.
uniform float left = 1.0;
uniform bool gilded = false;

const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
const vec3 METAL_CORE = vec3(1.0, 1.0, 1.0);         // metal core #FFFFFF
const vec3 WIND_HOT = vec3(0.918, 0.988, 0.965);     // wind hot #EAFCF6
const vec3 WIND_MID = vec3(0.706, 0.933, 0.878);     // wind mid #B4EEE0
const vec3 WIND_EDGE = vec3(0.310, 0.682, 0.620);    // wind edge #4FAE9E
const vec3 GOLD_HI = vec3(1.0, 0.941, 0.753);        // gold hi #FFF0C0
const float GRID = 2.0;
const vec2 KEY = vec2(-0.70710678, -0.70710678);
const float HEAD_LEN = 10.0;
const float HEAD_W = 6.0;

varying vec2 v_world;
varying flat vec2 v_origin;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	v_origin = MODEL_MATRIX[3].xy;
}

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

float sd_triangle(vec2 p, vec2 p0, vec2 p1, vec2 p2) {
	vec2 e0 = p1 - p0;
	vec2 e1 = p2 - p1;
	vec2 e2 = p0 - p2;
	vec2 v0 = p - p0;
	vec2 v1 = p - p1;
	vec2 v2 = p - p2;
	vec2 pq0 = v0 - e0 * clamp(dot(v0, e0) / dot(e0, e0), 0.0, 1.0);
	vec2 pq1 = v1 - e1 * clamp(dot(v1, e1) / dot(e1, e1), 0.0, 1.0);
	vec2 pq2 = v2 - e2 * clamp(dot(v2, e2) / dot(e2, e2), 0.0, 1.0);
	float s = sign(e0.x * e2.y - e0.y * e2.x);
	vec2 d = min(min(vec2(dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
		vec2(dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
		vec2(dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
	return -sqrt(d.x) * sign(d.y);
}

vec4 shade_cell(vec2 world, vec2 origin) {
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	vec2 p = centre - origin;
	float b = bayer4(cell);
	if (left < 1.0 && left <= b)
		return vec4(0.0);

	float best = 1e6;
	vec2 out_dir = vec2(1.0, 0.0);
	for (int k = 0; k < 4; k++) {
		float a = spin + 1.5707963 * float(k);
		vec2 o = vec2(cos(a), sin(a));
		vec2 side = vec2(-o.y, o.x);
		float sd = sd_triangle(p, o * ring, o * (ring + HEAD_LEN) + side * HEAD_W, o * (ring + HEAD_LEN) - side * HEAD_W);
		if (sd < best) {
			best = sd;
			out_dir = o;
		}
	}
	if (best <= 0.0) {
		vec2 side = vec2(-out_dir.y, out_dir.x);
		float along = dot(p, out_dir) - ring;
		float flank = dot(p, side) >= 0.0 ? 1.0 : -1.0;
		float lit = dot(side * flank, KEY);
		vec3 col;
		if (along < 3.0)
			col = gilded ? GOLD_HI : METAL_CORE;
		else if (best > -GRID && lit < 0.0)
			col = WIND_EDGE;
		else
			col = lit > 0.0 ? WIND_HOT : WIND_MID;
		return vec4(col, 1.0);
	}
	if (best <= GRID) {
		return vec4(OCC, 1.0);
	}
	return vec4(0.0);
}

void fragment() {
	// Godot allows no return in fragment(): the cell is shaded by shade_cell, and alpha 0 is nothing.
	vec4 c = shade_cell(v_world, v_origin);
	if (c.a < 0.5)
		discard;
	COLOR = c;
}
