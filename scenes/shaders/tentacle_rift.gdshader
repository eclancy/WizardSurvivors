// The ground under Black Tentacles: the rift the tentacles come out of, the scatter of rifts that
// Shadow Forest splinters it into, and Elder God Maw (scripts/BlackTentacles.cs).
//
// Drawn here rather than in _Draw because two of the three are areas full of detail - rings of
// teeth, forty eyes - that would be thousands of draw calls a frame as stamped squares, and because
// they have to be pixel art at ANY radius: the radius is the spell's hit radius, which Area scales
// continuously, so no pre-drawn sheet at an integer scale could match it. Every value is computed
// once per 2 px cell (the game's art pixel, .ai/art-direction.md section 1) and every colour is a
// Bonelight tone - occlusion, the darkness and poison emissive ramps, bone, living flesh, robe
// violet, earth - with Bayer dithering as the only gradient, as in water_surface.gdshader. Nothing
// is blended and nothing is antialiased.
//
// THE RADIUS IS THE HIT. `radius * open_amount` is exactly the slow's reach in BlackTentacles.Scan,
// and nothing here draws outside it except a few clods of earth while it is still opening, which
// land inside the full radius. The faint dotted ground out to that edge is the honest extent of
// the slow; the rift or maw sits inside it.
shader_type canvas_item;
render_mode unshaded;

uniform float radius = 90.0;
uniform float open_amount = 1.0;
uniform float time = 0.0;
uniform float seed = 0.0;
// 0 the rift, 1 Elder God Maw, 2 Shadow Forest.
uniform int face = 0;
// Shadow Forest: xy the centre of each small rift, z its radius at full open. Local px.
uniform vec4 pools[7];
uniform int pool_count = 0;
// Unit vector from the maw toward the player. Every eye on the rim watches him.
uniform vec2 look = vec2(1.0, 0.0);
// Seconds since the maw finished opening; the eyes open one by one from then.
uniform float maw_age = 0.0;
// A legendary cast: the lit lip is gold instead of darkness-hot.
uniform bool gilded = false;

const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
const vec3 DARK_EDGE = vec3(0.208, 0.125, 0.361);    // darkness edge #35205C
const vec3 DARK_MID = vec3(0.541, 0.361, 0.769);     // darkness mid #8A5CC4
const vec3 DARK_HOT = vec3(0.706, 0.549, 0.910);     // darkness hot #B48CE8
const vec3 ROBE_DEEP = vec3(0.078, 0.063, 0.122);    // robe violet deep #14101F
const vec3 ROBE_SHADE = vec3(0.133, 0.110, 0.200);   // robe violet shade #221C33
const vec3 ROBE_BASE = vec3(0.204, 0.173, 0.298);    // robe violet base #342C4C
const vec3 FLESH_SHADE = vec3(0.431, 0.267, 0.314);  // living flesh shade #6E4450
const vec3 FLESH_DEEP = vec3(0.235, 0.141, 0.212);   // living flesh deep #3C2436
const vec3 BONE_HI = vec3(0.933, 0.949, 0.980);      // bone hi #EEF2FA
const vec3 BONE_LIT = vec3(0.831, 0.863, 0.918);     // bone lit #D4DCEA
const vec3 BONE_BASE = vec3(0.604, 0.643, 0.722);    // bone base #9AA4B8
const vec3 BONE_SHADE = vec3(0.384, 0.431, 0.518);   // bone shade #626E84
const vec3 BONE_DEEP = vec3(0.220, 0.251, 0.310);    // bone deep #38404F
const vec3 POISON_EDGE = vec3(0.420, 0.580, 0.125);  // poison edge #6B9420
const vec3 POISON_MID = vec3(0.706, 0.878, 0.290);   // poison mid #B4E04A
const vec3 EARTH_EDGE = vec3(0.478, 0.314, 0.141);   // earth edge #7A5024
const vec3 EARTH_MID = vec3(0.788, 0.604, 0.369);    // earth mid #C99A5E
const vec3 GOLD_LIT = vec3(0.847, 0.690, 0.290);     // gold lit #D8B04A

const float GRID = 2.0;
// The key light, from the upper left (.ai/art-direction.md section 2).
const vec2 KEY = vec2(-0.70710678, -0.70710678);

varying vec2 local;

void vertex() {
	local = VERTEX;
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

float hash11(float n) {
	return fract(sin(n * 12.9898 + seed * 7.8233) * 43758.5453);
}

// t of the way from a to b, as an ordered dither rather than a blend.
vec3 dither(vec3 a, vec3 b, float t, vec2 cell) {
	return clamp(t, 0.0, 1.0) > bayer4(cell) ? b : a;
}

// Three harmonics and a slow crawl: one frequency reads as a wobble, three read as a shape.
float rim_wobble(float a, float s) {
	return sin(a * 3.0 + s) * 0.55 + sin(a * 5.0 - s * 1.7 + time * 0.8) * 0.3 + sin(a * 8.0 + s * 0.4 - time * 1.3) * 0.15;
}

// The infested ground, out to exactly the slow's radius: a sparse dither of darkness, thicker
// toward the edge, and a broken rim on the edge itself.
bool field(vec2 p, vec2 cell, float r, out vec3 col) {
	col = OCC;
	float d = length(p);
	if (d > r)
		return false;
	float a = atan(p.y, p.x);
	if (d > r - GRID) {
		float dashes = max(6.0, floor(r * 0.22));
		if (fract(a / TAU * dashes + time * 0.04) < 0.55) {
			col = DARK_MID;
			return true;
		}
		return false;
	}
	float density = 0.14 + 0.24 * smoothstep(0.5 * r, r, d);
	if (bayer4(cell) < density) {
		col = DARK_EDGE;
		return true;
	}
	return false;
}

// A rift: a hole in the ground with a lit lip, a crack of occlusion under it, and a dark that
// deepens to nothing, with flecks of rot rising out of it.
bool rift(vec2 p, vec2 cell, float rb, float s, out vec3 col) {
	col = OCC;
	if (rb < GRID)
		return false;
	float d = length(p);
	float a = atan(p.y, p.x);
	float edge = rb * (0.90 + 0.10 * rim_wobble(a, s));
	if (d > edge)
		return false;
	float inner = edge - d;
	vec2 dir = d > 0.01 ? p / d : vec2(0.0, 1.0);
	float lit = dot(dir, KEY);
	if (inner < GRID) {
		col = lit > 0.35 ? (gilded ? GOLD_LIT : DARK_HOT) : (lit < -0.3 ? DARK_EDGE : DARK_MID);
		return true;
	}
	if (inner < GRID * 2.0)
		return true; // OCC: the shadow under the lip
	float t = (inner - GRID * 2.0) / max(GRID, edge * 0.5);
	col = dither(DARK_EDGE, ROBE_DEEP, t * 2.2, cell);
	col = dither(col, OCC, t * 2.2 - 0.9, cell);
	vec2 rising = floor((p + vec2(0.0, time * 10.0)) / GRID);
	float h = hash12(rising + s * 17.0);
	if (t > 0.25 && h > 0.986)
		col = h > 0.996 ? POISON_MID : POISON_EDGE;
	return true;
}

// One eye on the maw's rim, in its own frame: x along the rim, y out from the centre.
// Deep and hollow: a lid of occlusion, a dark violet white, an iris barely lighter than it, and one
// dim pixel of rot for a glint. Nothing in it is bright - the brightest thing on the maw is its teeth.
bool eye(vec2 q, float hw, float openness, vec2 gaze, out vec3 col) {
	col = OCC;
	if (abs(q.x) > hw)
		return false;
	float lens = 1.0 - (q.x / hw) * (q.x / hw);
	float hh = hw * 0.55 * openness;
	if (hh < GRID * 0.75) {
		// Shut: a seam.
		return abs(q.y) < GRID * 0.5 && abs(q.x) < hw * 0.85;
	}
	float half_height = hh * lens;
	if (abs(q.y) > half_height)
		return false;
	if (abs(q.y) > half_height - GRID)
		return true; // the lid
	vec2 pupil = gaze * hw * 0.35;
	float iris = min(hh, hw) * 0.85;
	float dp = length(q - pupil);
	col = ROBE_SHADE;
	if (dp < iris)
		col = DARK_EDGE;
	if (dp < iris * 0.5)
		col = OCC;
	if (openness > 0.7 && length(q - (pupil + vec2(-GRID, -GRID) * 0.75)) < GRID * 0.6)
		col = POISON_EDGE;
	return true;
}

// Elder God Maw. A hole in the ground with a lip of torn earth and wet flesh, three rings of teeth
// receding into the dark - each ring slower, smaller and dimmer than the one in front of it, turning
// against its neighbours - and a throat with something still moving at the bottom. Round the rim, a
// ring of small eyes that open one at a time once the maw is open, blink on their own clocks, and
// all look at the player.
bool maw(vec2 p, vec2 cell, float rcur, out vec3 col) {
	col = OCC;
	float d = length(p);
	if (d > rcur)
		return false;
	float a = atan(p.y, p.x);
	vec2 dir = d > 0.01 ? p / d : vec2(0.0, 1.0);
	float lit = dot(dir, KEY);
	float breathe = sin(time * 1.6);
	float m = rcur * 0.80 * (1.0 + 0.02 * breathe);

	// The ring of eyes, between the lip and the edge of the slow.
	float ring_in = m + GRID * 2.0;
	if (d > ring_in) {
		float band = rcur - ring_in;
		float hw = clamp(band * 0.40, 3.0, 8.0);
		float er = ring_in + band * 0.5;
		float n = clamp(floor(TAU * er / (hw * 3.6)), 6.0, 40.0);
		float sector = floor((a / TAU + 0.5) * n);
		float ac = (sector + 0.5) / n * TAU - PI;
		vec2 normal = vec2(cos(ac), sin(ac));
		vec2 tangent = vec2(-normal.y, normal.x);
		vec2 q = vec2(dot(p - normal * er, tangent), dot(p - normal * er, normal));
		float order = hash11(sector + 3.1);
		float wake = clamp((maw_age - order * 2.4) / 0.6, 0.0, 1.0);
		float period = 3.0 + 5.0 * hash11(sector + 7.7);
		float ph = mod(time + hash11(sector + 1.3) * period, period);
		float blink = ph < 0.45 ? abs(ph - 0.225) / 0.225 : 1.0;
		vec2 gaze = vec2(dot(look, tangent), dot(look, normal));
		vec3 eye_col;
		if (maw_age > 0.0 && eye(q, hw, wake * blink, gaze, eye_col)) {
			col = eye_col;
			return true;
		}
		return field(p, cell, rcur, col);
	}

	// The lip: torn earth outside, wet flesh inside.
	if (d > m) {
		if (d > m + GRID) {
			float h = hash12(floor(p / GRID));
			col = lit > 0.2 && h > 0.55 ? EARTH_MID : (h > 0.4 ? EARTH_EDGE : OCC);
			return true;
		}
		col = lit > 0.3 ? FLESH_SHADE : FLESH_DEEP;
		if (lit > 0.45 && hash12(cell + floor(time * 2.0)) > 0.82)
			col = BONE_HI;
		return true;
	}

	float depth = 1.0 - d / m;
	// The throat wall behind the teeth, falling to nothing fast: most of the maw is dark, and what
	// the light finds is the near teeth on the lit side.
	col = dither(FLESH_DEEP, ROBE_SHADE, depth * 5.0 - 0.2, cell);
	col = dither(col, ROBE_DEEP, depth * 5.0 - 1.2, cell);
	col = dither(col, OCC, depth * 4.0 - 1.5, cell);

	// Three rings of teeth, each turning slowly against its neighbours. Every tooth is its own length -
	// some long, some broken off - and its tone is how deep its ring is plus which side of the maw
	// it is on, so the near teeth on the lit side are bone and everything else sinks into the dark.
	for (int k = 0; k < 3; k++) {
		float fk = float(k);
		float ripple = 1.0 + 0.04 * sin(time * 1.6 - fk * 0.8);
		float rk = m * (0.95 - 0.22 * fk) * ripple;
		float lk = m * (0.17 - 0.035 * fk);
		float nk = max(8.0, floor(TAU * rk / (16.0 - 2.0 * fk)));
		float spin = (mod(fk, 2.0) < 0.5 ? 1.0 : -1.0) * time * 0.012 * (1.0 + fk * 0.6) + seed + fk * 0.37;
		float turns = (a / TAU + spin) * nk;
		float tooth = floor(turns);
		float f = fract(turns);
		float tri = 1.0 - abs(2.0 * f - 1.0);
		float len = lk * (0.45 + 0.75 * hash11(tooth * 7.31 + fk * 113.0));
		float tip = rk - len * pow(tri, 0.6);
		if (d <= rk && d >= tip) {
			vec2 tang = vec2(-dir.y, dir.x) * (f < 0.5 ? -1.0 : 1.0);
			float stop = fk * 1.5 + (dot(tang, KEY) > 0.0 ? 0.0 : 1.0) + (lit > 0.25 ? 0.0 : (lit < -0.25 ? 2.0 : 1.0));
			if (stop < 0.5) col = BONE_LIT;
			else if (stop < 1.5) col = BONE_BASE;
			else if (stop < 2.5) col = BONE_SHADE;
			else if (stop < 3.5) col = BONE_DEEP;
			else if (stop < 4.5) col = ROBE_BASE;
			else col = ROBE_SHADE;
			// Wet: a glint on the tip of a near tooth now and then, catching the light as it breathes.
			if (k == 0 && lit > 0.25 && d < tip + GRID * 1.5 && hash12(cell + floor(time * 2.5)) > 0.75)
				col = BONE_HI;
			return true;
		}
		// The gum each ring grows out of.
		if (k > 0 && d > rk && d < rk + GRID)
			col = k == 1 ? FLESH_DEEP : ROBE_DEEP;
	}

	// The bottom: nothing, and something slow turning in it.
	if (depth > 0.5) {
		col = OCC;
		float spiral = fract(a / TAU * 3.0 + d * 0.035 - time * 0.05);
		if (spiral < 0.06 && bayer4(cell) < 0.4)
			col = ROBE_DEEP;
	}
	return true;
}

void fragment() {
	vec2 cell = floor(local / GRID);
	vec2 p = cell * GRID + GRID * 0.5;
	float rcur = radius * open_amount;
	vec3 col = OCC;
	bool hit = false;

	if (face == 1) {
		hit = maw(p, cell, rcur, col);
	} else if (face == 2) {
		for (int i = 0; i < 7; i++) {
			if (i >= pool_count)
				break;
			vec3 c;
			if (rift(p - pools[i].xy * open_amount, cell, pools[i].z * open_amount, seed + float(i) * 2.3, c)) {
				col = c;
				hit = true;
				break;
			}
		}
		if (!hit)
			hit = field(p, cell, rcur, col);
	} else {
		hit = rift(p, cell, rcur * 0.66, seed, col);
		if (!hit)
			hit = field(p, cell, rcur, col);
	}

	// Clods of earth thrown up while the ground is tearing open - inside the full radius only.
	if (!hit && open_amount < 0.999) {
		float d = length(p);
		if (d > rcur && d < min(radius, rcur + 12.0) && hash12(cell + floor(time * 18.0)) > 0.94) {
			col = hash12(cell) > 0.5 ? EARTH_MID : EARTH_EDGE;
			hit = true;
		}
	}

	if (!hit)
		discard;
	COLOR = vec4(col, 1.0);
}
