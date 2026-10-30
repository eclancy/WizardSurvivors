// Hunter's Draw's bow (scripts/BowDrawVisual.cs): the bow held out along the aim while the shot is drawn,
// its limbs bending and its string pulling back with the charge - as pixel art on the game's 2 px grid.
//
// It was drawn with antialiased polylines and lines - limbs, string, shaft, fletching, each on a
// translucent outline - a filled polygon for the head and two soft circles for the glow at full draw:
// the smooth vector look the Bonelight contract rules out, and turned with the aim onto a rotated grid.
// Now the bow is one quad in the node's frame, and every fragment snaps itself to the WORLD's 2 px cell
// first and only then turns that cell into the bow's frame (x along the aim, y across), as
// riptide_wave.gdshader does, so the art pixels stay square and upright at any aim. The pose still
// interpolates continuously with the charge, because the shapes are measured, not stamped.
//
// What it is, in opaque Bonelight tones with Bayer dithering the only gradient:
//   - the limbs: wood in the Earth ramp, lit on the flank toward the key from the upper left, darker
//     inside the curve and at the nocks; they throb toward Wind as the arms strain at full draw;
//   - the string: one art pixel of bone, the least important line, so the faintest;
//   - the arrow: the one thing that has to be unmistakable - it is the aim - so the brightest, and it
//     carries the charge in its colour (`ink`): Metal's dull stops while stopping would only relax the
//     bow, Wind's mid once stopping would loose it, Wind's hot and white at full draw;
//   - an art pixel of occlusion behind limbs and arrow, so the bow holds on sand and snow as well as on
//     dungeon stone;
//   - at full draw the head burns: a white-hot knot on the point and a dithered spray of hot round it,
//     throbbing with the strain - small, on the point, never a ring round the bow.
// The bow has no area and draws nothing on the ground.
shader_type canvas_item;
render_mode unshaded;

uniform float bow_radius = 26.0;
uniform float half_angle = 1.0821;
uniform float charge = 0.0;
// How far the nock is pulled back from the riser's line, px.
uniform float pull = 0.0;
uniform float arrow_length = 52.0;
// 0 stopping would only relax the bow .. 1 stopping would loose it .. 2 full draw; between 1 and 2 the
// two inks are dithered.
uniform float ink = 0.0;
uniform float strain = 0.0;
uniform bool full = false;
uniform bool gilded = false;

const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
const vec3 EARTH_HOT = vec3(0.910, 0.769, 0.549);    // earth hot #E8C48C
const vec3 EARTH_MID = vec3(0.788, 0.604, 0.369);    // earth mid #C99A5E
const vec3 EARTH_EDGE = vec3(0.478, 0.314, 0.141);   // earth edge #7A5024
const vec3 BONE_BASE = vec3(0.604, 0.643, 0.722);    // bone base #9AA4B8
const vec3 METAL_CORE = vec3(1.0, 1.0, 1.0);         // metal core #FFFFFF
const vec3 METAL_HOT = vec3(0.918, 0.949, 0.980);    // metal hot #EAF2FA
const vec3 METAL_MID = vec3(0.769, 0.831, 0.910);    // metal mid #C4D4E8
const vec3 METAL_EDGE = vec3(0.369, 0.447, 0.565);   // metal edge #5E7290
const vec3 WIND_HOT = vec3(0.918, 0.988, 0.965);     // wind hot #EAFCF6
const vec3 WIND_MID = vec3(0.706, 0.933, 0.878);     // wind mid #B4EEE0
const vec3 WIND_EDGE = vec3(0.310, 0.682, 0.620);    // wind edge #4FAE9E
const vec3 GOLD_HI = vec3(1.0, 0.941, 0.753);        // gold hi #FFF0C0
const float GRID = 2.0;
const vec2 KEY = vec2(-0.70710678, -0.70710678);
const float HEAD_LEN = 12.0;
const float HEAD_W = 6.5;
// Half the limbs' thickness, px: two to three art pixels across.
const float LIMB = 2.6;

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

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

float seg_dist(vec2 p, vec2 a, vec2 b) {
	vec2 ab = b - a;
	float t = clamp(dot(p - a, ab) / max(dot(ab, ab), 0.0001), 0.0, 1.0);
	return length(p - (a + ab * t));
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

// The limb's radius at angle a: a deeper draw bends it back toward the archer.
float limb_r(float a) {
	return bow_radius * (1.0 - 0.18 * charge * cos(a));
}

// Half the width, in px, of a line one art pixel thick at the angle whose normal (bow frame) is n.
float pixel_half(vec2 n, vec2 axis_x, vec2 axis_y) {
	vec2 w = normalize(n.x * axis_x + n.y * axis_y);
	return 0.5 * GRID * (abs(w.x) + abs(w.y));
}

vec4 shade_cell(vec2 world, vec2 origin, vec2 axis_x, vec2 axis_y) {
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	vec2 rel = centre - origin;
	vec2 p = vec2(dot(rel, axis_x), dot(rel, axis_y));
	vec2 key = vec2(dot(KEY, axis_x), dot(KEY, axis_y));
	float b = bayer4(cell);

	// The arrow's inks, by the charge: dithered from one to the next.
	float level = floor(ink) + (fract(ink) > b ? 1.0 : 0.0);
	vec3 shaft = level >= 2.0 ? WIND_HOT : (level >= 1.0 ? WIND_MID : METAL_EDGE);
	vec3 head_lit = level >= 2.0 ? METAL_CORE : (level >= 1.0 ? METAL_HOT : METAL_MID);
	vec3 head_dark = level >= 2.0 ? WIND_MID : (level >= 1.0 ? METAL_MID : METAL_EDGE);
	vec3 vane = level >= 2.0 ? WIND_MID : (level >= 1.0 ? WIND_EDGE : METAL_EDGE);
	if (gilded && level >= 1.0)
		head_lit = GOLD_HI;

	vec2 nock = vec2(-pull, 0.0);
	float tip_x = -pull + arrow_length;
	vec2 head_base = vec2(tip_x - HEAD_LEN, 0.0);

	// Full draw: the white-hot knot on the point, over everything.
	vec2 glow = vec2(tip_x - HEAD_LEN * 0.4, 0.0);
	if (full && length(p - glow) <= 4.5) {
		return vec4(METAL_CORE, 1.0);
	}

	// The limbs.
	float ap = atan(p.y, p.x);
	float rp = length(p);
	float d_limb;
	float along = abs(ap) / half_angle;
	if (abs(ap) <= half_angle) {
		d_limb = abs(rp - limb_r(ap));
	} else {
		float a = sign(ap) * half_angle;
		d_limb = length(p - vec2(cos(a), sin(a)) * limb_r(a));
	}
	vec2 tip_top = vec2(cos(half_angle), -sin(half_angle)) * limb_r(half_angle);
	vec2 tip_bottom = vec2(cos(half_angle), sin(half_angle)) * limb_r(half_angle);

	float thin_x = pixel_half(vec2(0.0, 1.0), axis_x, axis_y);
	float sd_head = sd_triangle(p, vec2(tip_x, 0.0), head_base + vec2(0.0, -HEAD_W), head_base + vec2(0.0, HEAD_W));
	float d_shaft = seg_dist(p, nock, head_base + vec2(1.0, 0.0));
	float d_vane = min(seg_dist(p, nock + vec2(9.0, 0.0), nock + vec2(1.0, -6.0)),
		seg_dist(p, nock + vec2(9.0, 0.0), nock + vec2(1.0, 6.0)));
	float vane_thin = GRID * 0.75;

	// The arrow over the limbs and the string: it is the aim.
	if (sd_head <= 0.0) {
		float flank = p.y >= 0.0 ? 1.0 : -1.0;
		float lit = dot(vec2(0.0, flank), key);
		vec3 col = lit > 0.0 ? head_lit : head_dark;
		if (p.x > tip_x - 3.0)
			col = level >= 1.0 ? METAL_CORE : METAL_MID;
		return vec4(col, 1.0);
	}
	if (d_shaft <= thin_x) {
		return vec4(shaft, 1.0);
	}
	if (d_vane <= vane_thin) {
		return vec4(vane, 1.0);
	}

	if (d_limb <= LIMB) {
		// Lit on the flank that faces the key, dark inside the curve, the nocks at the tips darkest.
		vec2 n = rp > 0.01 ? p / rp : vec2(1.0, 0.0);
		float outward = rp - (abs(ap) <= half_angle ? limb_r(ap) : rp);
		vec3 col = EARTH_MID;
		if (along > 0.9)
			col = EARTH_EDGE;
		else if (outward > 0.6)
			col = dot(n, key) > -0.2 ? EARTH_HOT : EARTH_MID;
		else if (outward < -0.6)
			col = EARTH_EDGE;
		// The arms tiring at full draw: the wood throbs toward the arrow's light.
		if (strain * 0.5 > b && outward > -0.6)
			col = WIND_MID;
		return vec4(col, 1.0);
	}

	// The string: one art pixel of bone from each tip to the nock. No backing - it is the faintest line.
	vec2 st = normalize(nock - tip_top);
	vec2 sb = normalize(nock - tip_bottom);
	if (seg_dist(p, tip_top, nock) <= pixel_half(vec2(-st.y, st.x), axis_x, axis_y)
		|| seg_dist(p, nock, tip_bottom) <= pixel_half(vec2(-sb.y, sb.x), axis_x, axis_y)) {
		return vec4(BONE_BASE, 1.0);
	}

	// An art pixel of occlusion behind the limbs and the arrow.
	if (sd_head <= GRID || d_shaft <= thin_x + GRID || d_vane <= vane_thin + GRID || d_limb <= LIMB + GRID) {
		return vec4(OCC, 1.0);
	}

	// Full draw: a spray of hot round the burning point, throbbing with the strain.
	if (full) {
		float halo = 9.0 + 3.0 * strain;
		if (length(p - glow) <= halo && b < 0.3 + 0.3 * strain) {
			return vec4(WIND_HOT, 1.0);
		}
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
