// Abyssal Scar (scripts/VoidScars.cs): the line a Void Lance lay along, torn into the ground as it
// withdraws, as pixel art on the game's 2 px grid.
//
// It was three stroked polylines a scar - a translucent violet band, a black one and a hairline -
// antialiased, at the lance's angle, which is the vector look the Bonelight contract rules out. Now each
// scar is one quad laid along its line and this shader decides every pixel of it: every fragment snaps
// to the WORLD's 2 px cell first and is then measured in the scar's own frame (x along it from the butt,
// y across), so the crack keeps square, upright art pixels at any angle, as in void_lance.gdshader. It
// is a crack in the ground in tentacle_rift.gdshader's manner: a lip lit by the key on the side that
// faces it, occlusion and robe violet's deep inside, the Darkness ramp for the light in it and for the
// hairline fissures that run off it, and a fleck of the lance's Arcane light rising out now and then.
// Every colour is opaque; Bayer dithering is the only gradient.
//
// THE CRACK IS THE CUT. `half_width` is VoidScars.HalfWidth and `reach` is HalfWidth + BodyAllowance,
// the distance VoidScars.Cut measures from the scar's line: the crack wanders inside half_width and the
// fissures off it end inside reach, so nothing is drawn where the scar does not cut, and there is no
// mark round it - the torn ground is the reach.
shader_type canvas_item;
render_mode unshaded;

uniform float scar_length = 100.0;
uniform float half_width = 5.0;
uniform float reach = 15.0;
// 0..1 tearing open over its first moments.
uniform float open = 1.0;
// 0..1 over its last third: it closes from both ends toward the middle and its light goes out.
uniform float closing = 0.0;
uniform float time = 0.0;
uniform float seed = 0.0;
uniform bool gilded = false;

const float GRID = 2.0;
const int KINKS = 9;

const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
const vec3 ARC_MID = vec3(0.753, 0.549, 1.0);        // arcane mid #C08CFF
const vec3 DARK_HOT = vec3(0.706, 0.549, 0.910);     // darkness hot #B48CE8
const vec3 DARK_MID = vec3(0.541, 0.361, 0.769);     // darkness mid #8A5CC4
const vec3 DARK_EDGE = vec3(0.208, 0.125, 0.361);    // darkness edge #35205C
const vec3 ROBE_DEEP = vec3(0.078, 0.063, 0.122);    // robe violet deep #14101F
const vec3 GOLD_LIT = vec3(0.847, 0.690, 0.290);     // gold lit #D8B04A
const vec2 KEY = vec2(-0.70710678, -0.70710678);

varying vec2 world;
varying flat vec2 origin;
varying flat vec2 axis_x;
varying flat vec2 axis_y;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	origin = MODEL_MATRIX[3].xy;
	axis_x = normalize(MODEL_MATRIX[0].xy);
	axis_y = normalize(MODEL_MATRIX[1].xy);
}

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

float hash11(float n) {
	return fract(sin(n * 12.9898 + seed * 7.8233) * 43758.5453);
}

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031 + seed * 0.0137);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

// Where the crack runs across the scar at x: kinked, wandering inside its half width, on the line at
// both ends. Rolled from the seed, so a closing scar keeps the shape it opened with.
float jag_at(float k) {
	if (k <= 0.0 || k >= float(KINKS))
		return 0.0;
	return (hash11(k) - 0.5) * 2.0 * half_width * 0.55;
}

float centre_at(float x) {
	float f = clamp(x / max(1.0, scar_length), 0.0, 1.0) * float(KINKS);
	float k = floor(f);
	return mix(jag_at(k), jag_at(k + 1.0), f - k);
}

float to_segment(vec2 p, vec2 a, vec2 b) {
	vec2 ab = b - a;
	float u = clamp(dot(p - a, ab) / max(0.0001, dot(ab, ab)), 0.0, 1.0);
	return length(p - a - ab * u);
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	vec2 rel = centre - origin;
	vec2 p = vec2(dot(rel, axis_x), dot(rel, axis_y));
	vec2 key = vec2(dot(KEY, axis_x), dot(KEY, axis_y));
	float b = bayer4(cell);

	// Closing from both ends toward the middle.
	float lo = closing * 0.5 * scar_length;
	float hi = scar_length - lo;
	if (p.x < lo || p.x > hi || abs(p.y) > reach)
		discard;

	vec3 col = OCC;
	bool hit = false;
	float yc = centre_at(p.x);
	float dy = p.y - yc;
	// The crack's half width: torn open, pointed at both ends, narrowing as it closes.
	float ends = min(1.0, min(p.x - lo, hi - p.x) / 14.0);
	float cw = (half_width - abs(yc) * 0.5) * open * ends * (1.0 - closing * 0.55);
	if (abs(dy) <= cw && cw >= GRID * 0.5) {
		hit = true;
		if (cw - abs(dy) < GRID) {
			// The lip: the side that faces the key is lit, the far side in shade.
			float lit = dot(vec2(0.0, sign(dy)), key);
			col = lit > 0.3 ? (gilded ? GOLD_LIT : DARK_HOT) : (lit < -0.3 ? DARK_EDGE : DARK_MID);
			if (closing > b * 1.4)
				col = DARK_EDGE;
		} else {
			// The dark inside, and now and then a fleck of the lance's light rising out of it.
			float t = (cw - abs(dy)) / max(GRID, cw);
			col = (t * 1.8 - 0.4) > b ? OCC : ROBE_DEEP;
			float h = hash12(floor((p + vec2(0.0, time * 9.0)) / GRID));
			if (h > 0.975 - 0.04 * (1.0 - closing))
				col = h > 0.992 ? ARC_MID : DARK_MID;
		}
	} else {
		// Hairline fissures running off the crack into the ground round it, one at most a kink, each
		// ending inside reach. They fade first as the scar closes.
		float f = p.x / max(1.0, scar_length) * float(KINKS);
		float k = floor(f);
		for (int j = -1; j <= 1; j++) {
			float kk = k + float(j);
			if (kk < 0.0 || kk >= float(KINKS))
				continue;
			float roll = hash11(kk + 40.0);
			if (roll < 0.45)
				continue;
			float x0 = (kk + 0.25 + 0.5 * hash11(kk + 80.0)) / float(KINKS) * scar_length;
			float dir = roll > 0.72 ? 1.0 : -1.0;
			float span = (reach - GRID) * (0.55 + 0.45 * hash11(kk + 120.0)) * open;
			vec2 a = vec2(x0, centre_at(x0));
			vec2 m = a + vec2(span * 0.35, dir * span * 0.55);
			vec2 e = vec2(x0 + span * 0.8, dir * (reach - GRID) * 0.95);
			e.y = dir * min(abs(e.y), span);
			float d = min(to_segment(p, a, m), to_segment(p, m, e));
			if (d < GRID * 0.75 && closing < 0.6 + 0.4 * b && e.x > lo && x0 < hi) {
				hit = true;
				col = d < GRID * 0.5 && open >= 1.0 && closing <= 0.0 ? DARK_MID : DARK_EDGE;
			}
		}
	}

	if (!hit)
		discard;
	COLOR = vec4(col, 1.0);
}
