// The Swallow-Hole (scripts/SwallowHole.cs; .ai/achievements-and-secrets.md 8.6): a sinkhole of black mud in
// the Blighted Swamp that takes whatever walks into it. One quad on the ground under the bodies, every
// fragment snapped to the WORLD's 2 px cell and shaded once, opaque, from the Bonelight rows - occlusion,
// stone, lichen, verdant's deep for peat, Darkness's edge for the pull - with Bayer dithering the only
// gradient (.ai/art-direction.md sections 1 and 3; the mire's way, mire_surface.gdshader).
//
// THE RADIUS IS THE TRIGGER. `hole_radius` is SecretLure.Radius: an enemy whose feet come within it is taken,
// and the player wading inside it is slowed and stung. The mud's bank wanders, but only INWARD of it
// (bank_in), so the hole never promises more than it takes - "a trap is an object whose edge is its
// trigger" (.ai/art-direction.md, "The player's spells carry no area markers", which a secret keeps too).
//
// WHAT IS DRAWN.
//   - The apron: from the rim out to `apron_radius` the bog goes black - a stain of occlusion, stone's deep
//     and peat, thick at the rim and thinning out by noise and dither into the floor. Never a ring with an
//     edge; it is the ground's near band of the far tell ("the ground goes from bog to black mud").
//   - The lip: lit along the down-right bank, where the wall faces the key; in shadow up-left.
//   - The mud: occlusion, turning. Arms of stone's deep and Darkness's edge wind in toward the middle on a
//     log spiral and drift inward with time - faster as it fills - so it reads as drawing in.
//   - Bubbles working up through it, and where something went in (`gulps`), the mud closing over it and a
//     ring rolling out.
//   - The glint at the bottom: the warm light, deep down, breathing; nearer the surface (brighter) the
//     fuller the hole is (`glint`). The one thing on screen that says how far along it is.
//   - When it is full (`heave` counts from that moment): the middle heaves up into a wet dome, breaks, and
//     the light comes out of it; then the mud lies still (`sated`): no pull, no bubbles, no glint.
shader_type canvas_item;
render_mode unshaded;

uniform vec2 centre = vec2(0.0);
uniform float hole_radius = 80.0;
uniform float apron_radius = 300.0;
uniform float time = 0.0;
// 0..1: how many it has taken of what it needs. Speeds the pull.
uniform float fullness = 0.0;
// How bright the light at the bottom is, 0..1 (0: gone).
uniform float glint = 0.0;
// Seconds since it filled; negative until then.
uniform float heave = -1.0;
// 1 once it has given the light up and gone still.
uniform float sated = 0.0;
// Where things went in: xy the world point, z the moment (on `time`), w a seed.
uniform vec4 gulps[8];
uniform int gulp_count = 0;

const float GRID = 2.0;
// The key light, from the upper left (.ai/art-direction.md section 2).
const vec2 KEY = vec2(-0.70710678, -0.70710678);

const vec3 OCC = vec3(0.020, 0.027, 0.047);          // occlusion #05070C - the mud
const vec3 ST_DEEP = vec3(0.043, 0.059, 0.094);      // stone deep #0B0F18
const vec3 ST_SHADE = vec3(0.078, 0.102, 0.149);     // stone shade #141A26
const vec3 ST_BASE = vec3(0.118, 0.149, 0.204);      // stone base #1E2634 - the heaving dome
const vec3 PEAT = vec3(0.071, 0.165, 0.039);         // verdant deep #122A0A - peat in the black
const vec3 MURK = vec3(0.086, 0.157, 0.180);         // lichen deep #16282E
const vec3 LI_SHADE = vec3(0.141, 0.251, 0.290);     // lichen shade #24404A - a wet sheen in shadow
const vec3 LI_BASE = vec3(0.165, 0.306, 0.345);      // lichen base #2A4E58 - the lit lip
const vec3 LI_LIT = vec3(0.227, 0.392, 0.439);       // lichen lit #3A6470 - a bubble bursting
const vec3 PULL = vec3(0.208, 0.125, 0.361);         // darkness edge #35205C - the pull's arms
const vec3 GO_SHADE = vec3(0.329, 0.247, 0.063);     // gold shade #543F10
const vec3 GO_BASE = vec3(0.541, 0.416, 0.118);      // gold base #8A6A1E
const vec3 GO_LIT = vec3(0.847, 0.690, 0.290);       // gold lit #D8B04A
const vec3 GO_HI = vec3(1.0, 0.941, 0.753);          // gold hi #FFF0C0

varying vec2 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

// How far in from the trigger radius the bank sits here: 0..6 px, fixed in world space.
float bank_in(vec2 p) {
	return 6.0 * (0.65 * vnoise(p / 22.0) + 0.35 * vnoise(p / 9.0 + vec2(13.1, 7.7)));
}

// --- the mud ---
vec3 mud(vec2 p, vec2 cell, float b, vec2 rel, float d, float bank, float facing, vec2 out_dir) {
	vec3 col;
	float depth = bank - d;
	float ang = atan(rel.y, rel.x);
	col = OCC;
	if (sated < 0.5) {
		// The pull: three arms on a log spiral, drifting inward (the phase falls as time runs), faster
		// as it fills. Darkness's edge near the middle, stone's deep further out.
		float spin = time * (0.12 + 0.30 * fullness);
		float arm = fract(3.0 * ang / TAU - 1.25 * log(max(d, 4.0) / hole_radius) - spin);
		float width = 0.16 + 0.10 * (d / hole_radius);
		if (arm < width) {
			col = d < hole_radius * 0.5 ? PULL : ST_DEEP;
			// The arm's leading edge catches the light where it faces the key.
			if (arm < 0.04 && facing > 0.2 && d > 12.0)
				col = LI_SHADE;
		}
		// The eye: the deepest point, where things go.
		if (d < 10.0)
			col = OCC;
	} else {
		// Still: a black pool with peat stirred through it, and a still sheen or two.
		float m = vnoise(p / 7.0);
		col = m > 0.66 ? ST_DEEP : OCC;
		if (mod(cell.y, 5.0) < 1.0 && hash12(vec2(floor(cell.x / 6.0), cell.y)) > 0.82 && facing > 0.0)
			col = LI_SHADE;
	}

	// The inner wall: lit where it faces the key, a shadow under the up-left bank.
	if (depth < 3.0)
		col = facing > 0.35 ? LI_BASE : (facing < -0.35 ? OCC : ST_DEEP);
	else if (depth < 6.0 && facing < -0.4)
		col = OCC;

	if (sated < 0.5 && heave < 0.0) {
		// Bubbles working up through it: swell, burst, a ring.
		for (int k = 0; k < 3; k++) {
			vec2 hk = vec2(float(k) * 3.7 + 1.3, float(k) * 11.3 + 0.7);
			float period = 1.9 + 1.1 * hash12(hk + 9.7);
			float cycle = floor(time / period + hash12(hk + 2.3));
			float ph = fract(time / period + hash12(hk + 2.3));
			// Each bubble comes up somewhere new each time.
			vec2 at = centre + (vec2(hash12(hk + cycle), hash12(hk + cycle + 5.1)) * 2.0 - 1.0) * hole_radius * 0.55;
			vec2 r = p - at;
			float rd = length(r);
			if (ph < 0.5) {
				float rad = 1.0 + 3.0 * (ph / 0.5);
				if (rd < rad) col = (rd > rad - 2.0) ? (dot(r, KEY) > 0.0 ? LI_BASE : LI_SHADE) : ST_DEEP;
			} else if (ph < 0.56) {
				if (rd < 3.0) col = LI_LIT;
			} else {
				float t = (ph - 0.56) / 0.44;
				float ring = length(r * vec2(1.0, 1.7));
				float rr = 4.0 + 12.0 * t;
				if (abs(ring - rr) < 1.2 && (1.0 - t) > b) col = t < 0.3 ? LI_BASE : LI_SHADE;
			}
		}
	}

	// Where something went in: the mud closing over it, and a ring rolling out.
	for (int i = 0; i < 8; i++) {
		if (i >= gulp_count) break;
		float a = time - gulps[i].z;
		if (a < 0.0 || a > 1.0) continue;
		vec2 r = (p - gulps[i].xy) * vec2(1.0, 1.6);
		float rd = length(r);
		if (rd < 9.0 * (1.0 - a))
			col = a < 0.2 ? ST_SHADE : OCC;
		float rr = 6.0 + 26.0 * a;
		if (abs(rd - rr) < 1.4 && (1.0 - a) * 1.1 > b)
			col = a < 0.35 ? LI_LIT : LI_SHADE;
	}

	// The light at the bottom, breathing - brighter the fuller it is.
	if (glint > 0.0 && heave < 0.0) {
		float breath = (0.5 - 0.5 * cos(time * TAU / 2.4)) * glint;
		vec2 g = floor(centre / GRID);
		vec2 o = cell - g;
		if (o.x >= -1.0 && o.x <= 0.0 && o.y >= -1.0 && o.y <= 0.0)
			col = breath > 0.62 ? GO_HI : (breath > 0.3 ? GO_LIT : GO_BASE);
		else if (abs(o.x + 0.5) + abs(o.y + 0.5) < 2.6 && breath * 1.2 - 0.1 > b)
			col = breath > 0.62 ? GO_LIT : GO_SHADE;
	}

	// Full: the middle heaves up into a wet dome, breaks, and the light comes out.
	if (heave >= 0.0 && heave < 1.2) {
		float dome = hole_radius * 0.55 * clamp(heave / 0.6, 0.0, 1.0);
		if (heave < 0.75 && d < dome) {
			float h = 1.0 - d / max(1.0, dome);
			col = h > 0.6 ? ST_BASE : (h > 0.25 ? ST_SHADE : ST_DEEP);
			if (dot(out_dir, KEY) > 0.3 && h > 0.2 && h < 0.7)
				col = LI_BASE;
			if (heave > 0.5 && h > 0.85)
				col = GO_LIT;
		} else if (heave >= 0.75) {
			// It broke: the light in the middle, and the mud slumping back in a ring.
			float t = (heave - 0.75) / 0.45;
			float rr = dome * (0.6 + 0.6 * t);
			if (abs(d - rr) < 2.0 && (1.0 - t) > b) col = LI_SHADE;
			if (d < 6.0 * (1.0 - t)) col = t < 0.4 ? GO_HI : GO_LIT;
		}
	}
	return col;
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 p = cell * GRID + GRID * 0.5;
	float b = bayer4(cell);
	vec2 rel = p - centre;
	float d = length(rel);
	if (d > apron_radius)
		discard;
	vec2 out_dir = d > 0.5 ? rel / d : vec2(0.0, 1.0);
	// Which way the wall here faces: +1 toward the key (lit), -1 away (in its own shadow).
	float facing = -dot(out_dir, KEY);
	float bank = hole_radius - bank_in(p);
	vec3 col;

	if (d >= bank) {
		// The apron: the bog going black toward the hole, a stain and never a ring.
		float t = clamp((d - hole_radius) / max(1.0, apron_radius - hole_radius), 0.0, 1.0);
		float n = 0.6 * vnoise(p / 22.0) + 0.4 * vnoise(p / 9.0 + vec2(3.3, 1.9));
		// Solid black near the hole, its edge ragged with the noise - tongues of it reaching out into the bog
		// and islands of bog left in it - and only a narrow dithered fringe where it gives out.
		float cover = clamp((0.5 - t + (n - 0.5) * 0.7) * 5.0 + 0.5, 0.0, 1.0);
		if (d - bank < 4.0)
			cover = 1.0;
		if (cover <= b)
			discard;
		float lip = d - bank;
		if (lip < 4.0) {
			// The lip: the down-right bank faces the key and its wet edge catches it.
			col = facing > 0.35 ? (lip < 2.0 ? LI_BASE : LI_SHADE) : (facing < -0.35 ? OCC : ST_DEEP);
		} else {
			float m = vnoise(p / 5.0 + vec2(1.7, 4.2));
			col = m > 0.62 ? ST_DEEP : (m < 0.34 ? PEAT : OCC);
			// The odd wet glint in the stain, on the lit faces of its lumps.
			if (hash12(cell + vec2(17.0, 3.0)) > 0.985 && facing > -0.2)
				col = LI_SHADE;
		}
	} else {
		col = mud(p, cell, b, rel, d, bank, facing, out_dir);
	}
	COLOR = vec4(col, 1.0);
}
