// Hunter's Draw's arrows, as pixel art on the game's 2 px grid: the arrow in flight (scripts/HuntersArrowLook.cs
// on ElementalBolt), Rain of Arrows' volley climbing and falling and standing in the ground
// (scripts/ArrowRain.cs) and Barbed's arrows standing in the enemies they stopped in (scripts/LodgedArrows.cs).
//
// The arrow in flight was a 32x32 sprite at a scale of 1.5 - a fractional scale, so its pixels were
// three device pixels and a half - turned to its heading and multiplied by an over-bright cyan, trailed
// by translucent particles; the rain and the lodged arrows were stroked lines, polylines and polygons, a
// translucent outline under each. Now every fragment snaps itself to the WORLD's 2 px cell first and only
// then turns that cell into the arrow's frame (x toward the point, y across), as riptide_wave.gdshader
// does, so the art pixels stay square and upright at any heading. One art pixel of occlusion backs every
// part (#05070C, never black), so an arrow holds over a body and on the pale floors as the bow does. The
// head is the Metal ramp - white at the point, its lit flank toward the key from the upper left, its
// edge stop on the other - the shaft and the fletching are Wind, the spell's two elements; in flight a
// wake of Wind streaks runs back off the fletching. Every colour is opaque; Bayer dithering is the only
// gradient, and the only way anything fades.
//
// THE POINT IS THE HIT. In flight the quad is the bolt's own (mode A, `world_quads` false): the arrow's
// frame is the node's, so an evolution's scale and Heavy Draw's grow the arrow and the collision circle
// together, and the point is put on the front of that circle (`hit_radius`, ElementalBolt.HitRadius):
// what the point touches is what the arrow hits. The rain and the lodged arrows are drawn where their
// own numbers put them - the spot a falling arrow strikes, the wound an arrow stands in.
//
// MANY ARROWS, ONE NODE (mode B, `world_quads` true): ArrowRain and LodgedArrows draw each arrow as a
// quad laid in world space round its point, the offset from the point in the UV (mire_surface's way),
// and the arrow's numbers in its vertex colour:
//   r  its heading, / TAU
//   g  how much of it is buried - in the ground, or in the body it stands in - / 64 px from the point
//   b  how much of it is left: 1 whole .. 0 gone, a Bayer dissolve
//   a  LodgedArrows only: 0, or how far a drop of blood has fallen from the wound (0..1]
// The node's modulate is overridden in both modes; a Legendary's gold is `gilded`, on the head.
shader_type canvas_item;
render_mode unshaded;

uniform bool world_quads = false;
// Mode A: the collision circle's radius in the node's units. The point sits on its front.
uniform float hit_radius = 8.0;
// The arrow's length, point to nock, in the arrow's units.
uniform float arrow_length = 52.0;
// Mode A: the wake of wind behind a flying arrow. Mode B: the wound and its blood (LodgedArrows).
uniform bool wake = false;
uniform bool wound = false;
uniform float seed = 0.0;
uniform bool gilded = false;

const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
const vec3 METAL_CORE = vec3(1.0, 1.0, 1.0);         // metal core #FFFFFF
const vec3 METAL_HOT = vec3(0.918, 0.949, 0.980);    // metal hot #EAF2FA
const vec3 METAL_MID = vec3(0.769, 0.831, 0.910);    // metal mid #C4D4E8
const vec3 METAL_EDGE = vec3(0.369, 0.447, 0.565);   // metal edge #5E7290
const vec3 WIND_HOT = vec3(0.918, 0.988, 0.965);     // wind hot #EAFCF6
const vec3 WIND_MID = vec3(0.706, 0.933, 0.878);     // wind mid #B4EEE0
const vec3 WIND_EDGE = vec3(0.310, 0.682, 0.620);    // wind edge #4FAE9E
const vec3 GOLD_HI = vec3(1.0, 0.941, 0.753);        // gold hi #FFF0C0
const vec3 GOLD_LIT = vec3(0.847, 0.690, 0.290);     // gold lit #D8B04A
const vec3 BLOOD_LIT = vec3(0.416, 0.125, 0.157);    // cloth red lit #6A2028
const vec3 BLOOD_BASE = vec3(0.290, 0.094, 0.125);   // cloth red base #4A1820
const float GRID = 2.0;
const vec2 KEY = vec2(-0.70710678, -0.70710678);
// The head: its length back from the point and its half-width at the barbs (BowDrawVisual's).
const float HEAD_LEN = 12.0;
const float HEAD_W = 6.0;

varying vec2 v_world;
varying vec2 v_offset;
varying flat vec4 v_data;
varying flat vec2 v_origin;
varying flat vec2 v_axis_x;
varying flat vec2 v_axis_y;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	v_offset = UV;
	v_data = COLOR;
	v_origin = MODEL_MATRIX[3].xy;
	v_axis_x = MODEL_MATRIX[0].xy;
	v_axis_y = MODEL_MATRIX[1].xy;
}

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031 + seed * 0.0137);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

mat2 rot(float a) {
	float c = cos(a);
	float s = sin(a);
	return mat2(vec2(c, s), vec2(-s, c));
}

float seg_dist(vec2 p, vec2 a, vec2 b) {
	vec2 ab = b - a;
	float t = clamp(dot(p - a, ab) / max(dot(ab, ab), 0.0001), 0.0, 1.0);
	return length(p - (a + ab * t));
}

// Signed distance to a triangle (negative inside).
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

vec4 shade_cell(vec2 world, vec2 offset, vec4 data, vec2 origin, vec2 axis_x, vec2 axis_y) {
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	float b = bayer4(cell);

	// The cell in the arrow's frame, with the point at the origin; one art pixel in the arrow's units;
	// the arrow's normal and the key light, in the world and in the arrow's frame.
	vec2 p;
	float art;
	vec2 normal_w;
	vec2 key;
	float bury = 0.0;
	float left = 1.0;
	float drop = 0.0;
	vec2 off = vec2(0.0);
	float heading = 0.0;
	if (world_quads) {
		off = offset + (centre - world);
		heading = data.r * TAU;
		p = rot(-heading) * off;
		art = GRID;
		normal_w = vec2(-sin(heading), cos(heading));
		key = rot(-heading) * KEY;
		bury = data.g * 64.0;
		left = data.b;
		drop = data.a;
	} else {
		mat2 m = mat2(axis_x, axis_y);
		mat2 to_node = inverse(m);
		p = to_node * (centre - origin) - vec2(hit_radius, 0.0);
		art = GRID / max(length(axis_x), 0.001);
		normal_w = normalize(axis_y);
		key = normalize(to_node * KEY);
	}
	// Dissolving: what is left of it, by the Bayer matrix.
	if (left < 1.0 && left <= b)
		return vec4(0.0);

	// Barbed's wound: a bead of blood where the shaft goes in, and a drop falling from it each tick -
	// upright, in world px, on the grid.
	if (world_quads && wound) {
		vec2 at = rot(heading) * vec2(-bury, 0.0);
		if (length(off - at) <= GRID * 1.2) {
			return vec4(BLOOD_LIT, 1.0);
		}
		if (drop > 0.0) {
			vec2 d = floor((off - at - vec2(-GRID * 0.5, GRID * 1.5 + 14.0 * drop)) / GRID);
			if (d.x == 0.0 && (d.y == 0.0 || d.y == 1.0) && (1.0 - drop * 0.6) > b) {
				return vec4(d.y == 0.0 ? BLOOD_LIT : BLOOD_BASE, 1.0);
			}
		}
	}

	// What is buried - in the ground, or in the body it stands in - is not drawn, nor its backing.
	if (world_quads && p.x > -bury)
		return vec4(0.0);

	float thin = 0.5 * GRID * (abs(normal_w.x) + abs(normal_w.y)) * (art / GRID);
	float tail = -arrow_length;
	float sd_head = sd_triangle(p, vec2(0.0, 0.0), vec2(-HEAD_LEN, HEAD_W), vec2(-HEAD_LEN, -HEAD_W));
	float sd_shaft = seg_dist(p, vec2(-HEAD_LEN + 1.0, 0.0), vec2(tail, 0.0)) - thin;
	float side = p.y >= 0.0 ? 1.0 : -1.0;
	float sd_vane = sd_triangle(p, vec2(tail + 11.0, 0.0), vec2(tail + 1.0, side * 6.0), vec2(tail + 3.0, 0.0));
	float sd = min(sd_head, min(sd_shaft, sd_vane));

	if (sd <= 0.0) {
		vec3 col;
		// Which flank faces the key.
		float lit = dot(vec2(0.0, side), key);
		if (sd_head <= 0.0) {
			if (p.x > -3.0)
				col = gilded ? GOLD_HI : METAL_CORE;
			else if (sd_head > -art && lit < 0.0)
				col = gilded ? GOLD_LIT : METAL_EDGE;
			else
				col = lit > 0.0 ? (gilded ? GOLD_HI : METAL_HOT) : (gilded ? GOLD_LIT : METAL_MID);
		} else if (sd_vane <= 0.0) {
			col = (sd_vane > -art && lit > 0.0) ? WIND_MID : WIND_EDGE;
		} else {
			col = WIND_MID;
		}
		return vec4(col, 1.0);
	}
	if (sd <= art) {
		return vec4(OCC, 1.0);
	}

	// In flight: a wake of wind streaks running back off the fletching, broken into dashes that slide
	// back as it goes, thinning out with distance.
	if (wake) {
		float x = tail - p.x;
		float row = round(p.y / (art * 1.5));
		if (x > 0.0 && x < arrow_length * 0.8 && abs(row) <= 1.0 && row != 0.0 && abs(p.y - row * art * 1.5) <= thin) {
			float dash = fract((x + TIME * 160.0) / 16.0 + hash12(vec2(row, 4.0)));
			float u = x / (arrow_length * 0.8);
			if (dash < 0.5 && (1.0 - u) > b) {
				return vec4(u < 0.4 ? WIND_MID : WIND_EDGE, 1.0);
			}
		}
	}
	return vec4(0.0);
}

void fragment() {
	// Godot allows no return in fragment(): the cell is shaded by shade_cell, and alpha 0 is nothing.
	vec4 c = shade_cell(v_world, v_offset, v_data, v_origin, v_axis_x, v_axis_y);
	if (c.a < 0.5)
		discard;
	COLOR = c;
}
