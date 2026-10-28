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
// and nothing here draws outside it except a few clods of earth while the rift and Shadow Forest are
// still opening, which land inside the full radius. The maw draws nothing outside it at all: its
// cracks run out exactly as far as the slow has, and the earth it throws is BlackTentacles'. The THING is the extent of the slow - no mark round it (2026-10-23,
// "remove aoe markers from abilities, their animations should make it clear what the hitbox is"):
// the rift opens to the whole radius, the maw's ring of eyes reaches it, and Shadow Forest's rifts
// are laid out so their outer lips are on it. A dithered disc with a dashed rim used to fill the
// ground between the rift (two thirds of the radius) and the edge of the slow; that was the marker.
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
// How far through its summon the maw is, 0 at the cast to 1 once open (and after).
uniform float summon = 1.0;
// A legendary cast: the lit lip is gold instead of darkness-hot.
uniform bool gilded = false;

// The maw's summon and shape, shared with BlackTentacles (MawBreach, MawHole, MawRim, MawDome) - move
// them together. Of the summon: the ground heaves until BREACH and the maw has risen by RISEN. Of the
// radius: the teeth end at HOLE and the torn rim round them at RIM; the eyes have the rest. DOME is
// how far the heave reaches, of the slow's reach at the moment.
const float BREACH = 0.30;
const float RISEN = 0.85;
const float HOLE = 0.74;
const float RIM = 0.86;
const float DOME = 0.62;

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
const vec3 EARTH_HOT = vec3(0.910, 0.769, 0.549);    // earth hot #E8C48C
// The dark soil under the earth ramp, as in gravewell_collapse.gdshader: the gold row's lower stops.
const vec3 SOIL_SHADE = vec3(0.329, 0.247, 0.063);   // gold shade #543F10
const vec3 SOIL_DEEP = vec3(0.180, 0.133, 0.031);    // gold deep #2E2208
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

// --- Elder God Maw ------------------------------------------------------------------------------
//
// It breaks up through the earth (2026-10-28). Before the breach the ground cracks and heaves
// (heave); at the breach the heaved plates are thrown aside - BlackTentacles draws them in the air
// and where they land, so nothing here draws them - and from then the maw rises out of the hole it
// made (rise, 0 to 1): the hole widens, the teeth grow out of the dark and catch the light, and the
// broken ground is tipped up round the lip into the torn rim it keeps for the rest of its life.

// Before the breach (h 0..1 through it): the ground over the maw splits from the centre out to the
// edge of the slow - every other crack all the way - and the middle of it heaves on what is coming
// up under it. Two rings and the spokes cut it into plates, the inner ones the higher, so a ring's
// inner lip catches the key light on the near side of the dome and throws a shadow outward on the far
// side; the cracks widen toward the top and breathe darkness; the soil shows where the top is
// splitting; and from halfway the dome trembles, an art pixel this way or that. The plates' own
// faces are the stage's ground, left undrawn, so what heaves is the ground the player was standing on.
bool heave(vec2 p, vec2 cell, float rcur, float h, out vec3 col) {
	col = OCC;
	float dome = rcur * DOME;
	if (h > 0.5 && length(p) < dome) {
		float beat = floor(time * 20.0);
		p -= floor(vec2(hash11(beat), hash11(beat + 5.3)) * 3.0) * GRID - GRID;
	}
	float d = length(p);
	if (d > rcur)
		return false;
	float a = atan(p.y, p.x);
	vec2 dir = d > 0.01 ? p / d : vec2(0.0, 1.0);
	float lit = dot(dir, KEY);
	float lift = d < dome ? h * (1.0 - d / dome) : 0.0;

	// The spokes, as the Gravewell's: one a sector, each wandering at its own angle in it.
	float n = clamp(floor(radius / 16.0), 7.0, 14.0);
	float sector = floor((a / TAU + 0.5) * n);
	float hs = hash11(sector + 1.7);
	float ac = (sector + 0.5 + (hs - 0.5) * 0.8) / n * TAU - PI;
	float off = a - ac;
	off -= TAU * floor((off + PI) / TAU);
	float zig = sin(d * 0.23 + hs * 17.0) * 1.6 + sin(d * 0.061 + hs * 5.0) * 5.0 * (d / radius);
	float lateral = off * d - zig;
	float reach = mod(sector, 2.0) < 0.5 ? rcur : rcur * (0.4 + 0.4 * hash11(sector + 9.1));
	vec2 across = vec2(-dir.y, dir.x) * sign(lateral);
	float width = GRID * (0.75 + 1.25 * lift);
	bool crack = false;
	bool lip = false;
	bool shadow = false;
	if (d < reach) {
		if (abs(lateral) < width)
			crack = true;
		else if (abs(lateral) < width + GRID && dot(across, KEY) < 0.0)
			lip = true; // the wall of the crack that faces back toward the light
	}
	// Two rings round the dome. The inside of each is the higher, more so as it heaves.
	for (int k = 0; k < 2; k++) {
		float fk = float(k);
		float rr = dome * (k == 0 ? 0.5 : 1.0) * (1.0 + 0.07 * sin(a * (3.0 + fk * 2.0) + seed * (1.0 + fk)));
		float dd = d - rr;
		float step_up = h * (k == 0 ? 0.85 : 0.45);
		if (abs(dd) < width)
			crack = true;
		else if (dd < 0.0 && dd > -width - GRID && lit > 0.2)
			lip = true;
		else if (dd > 0.0 && lit < -0.2 && dd < width + GRID * (1.0 + 2.0 * step_up * -lit))
			shadow = true;
	}

	// The top of the dome splitting open: clumps of soil, and darkness in the cracks through it.
	float split = dome * 0.3 * smoothstep(0.35, 1.0, h);
	if (d < split) {
		float k = hash12(floor(cell / 2.0) + 3.0);
		col = k > 0.62 ? (lit > 0.0 ? EARTH_MID : EARTH_EDGE) : (k > 0.28 ? SOIL_SHADE : SOIL_DEEP);
		if (crack)
			col = bayer4(cell) < h * 0.3 ? DARK_MID : OCC;
		return true;
	}
	if (crack) {
		// Darkness breathing out of the cracks over the dome, more of it as it heaves: a glimmer, not
		// a fill - the cracks are black first.
		if (lift > 0.0 && bayer4(cell + vec2(floor(time * 8.0), 0.0)) < lift * 0.45)
			col = lift > 0.7 ? DARK_MID : DARK_EDGE;
		return true;
	}
	if (lip) {
		col = lift > 0.25 ? EARTH_MID : EARTH_EDGE;
		return true;
	}
	return shadow; // OCC
}

// Where the crack between slab j and slab j - 1 sits, off even: up to a third of a slab either way.
float slab_jitter(float j, float n) {
	return (hash11(mod(j, n) + 41.0) - 0.5) * 0.6;
}

// The torn rim, from the hole's lip out (and past it, the cracks). The ground the maw came up through
// is broken into slabs and tipped up round the lip, inner edge raised. A slab's top faces out and up,
// so the slabs on the key side of the ring catch the light and the far ones sit in shade; its broken
// inner face looks back across the hole, so it is the far side's faces that are lit; its outer edge
// is torn from the ground, and on the far side of the ring the raised slab throws a shadow past it.
// Between the slabs, cracks, and every other one runs on through the ground toward the edge of the
// slow - every second of those all the way, so the edge reads as where the breaking stopped, not as a
// ring. The slabs are as deep as the rising maw has pushed them.
bool torn_rim(vec2 p, vec2 cell, float d, float a, float lit, float m, float rcur, float rise, out vec3 col) {
	col = OCC;
	// The maw's own lip: wet flesh, a wet glint on the lit side.
	if (d < m + GRID) {
		col = lit > 0.3 ? FLESH_SHADE : FLESH_DEEP;
		if (lit > 0.45 && hash12(cell + floor(time * 2.0)) > 0.82)
			col = BONE_HI;
		return true;
	}
	// Which slab. The count comes from the full radius, so it holds still while the maw rises.
	float n = clamp(floor(TAU * radius * HOLE / 26.0), 9.0, 26.0);
	float u = (a / TAU + 0.5) * n + 0.06 * sin(d * 0.21 + seed * 3.0);
	float k = floor(u);
	float b0 = k + slab_jitter(k, n);
	float b1 = k + 1.0 + slab_jitter(k + 1.0, n);
	float slab = k;
	if (u < b0) {
		slab = k - 1.0;
		b1 = b0;
		b0 = k - 1.0 + slab_jitter(k - 1.0, n);
	} else if (u >= b1) {
		slab = k + 1.0;
		b0 = b1;
		b1 = k + 2.0 + slab_jitter(k + 2.0, n);
	}
	float id = mod(slab, n);
	float unit = TAU * d / n;
	float from0 = (u - b0) * unit;
	float from1 = (b1 - u) * unit;
	float edge = min(from0, from1);
	// Every slab its own: most jut, one in six has crumbled to a stub, and each is broken off on a
	// slant across its width rather than square. The outer edge then steps an art pixel or two in or
	// out every few, so it is torn, not cut - and never reaches the eyes.
	float across_slab = clamp((u - b0) / max(0.01, b1 - b0), 0.0, 1.0);
	float stub = hash11(id + 13.0) < 0.17 ? 0.35 : 0.7 + 0.45 * hash11(id + 19.0);
	float slant = 1.0 + (hash11(id + 29.0) - 0.5) * 1.1 * (across_slab - 0.5);
	float depth = rcur * (RIM - HOLE) * mix(0.55, 1.0, rise) * stub * slant;
	float jag = floor(hash12(vec2(id * 7.0, floor(u * 9.0))) * 4.0) - 1.5;
	float outer = min(m + GRID + depth + jag * GRID, rcur * (RIM + 0.03));

	if (d <= outer) {
		if (edge < GRID * (0.75 + 0.5 * rise))
			return true; // OCC: the crack between two slabs
		if (d > outer - GRID)
			return true; // OCC: the edge it tore from
		if (d - m - GRID < GRID * (1.0 + rise)) {
			// The broken inner face, looking back across the hole toward the light.
			float facing = -lit;
			col = facing > 0.35 ? EARTH_HOT : (facing > -0.15 ? EARTH_EDGE : SOIL_DEEP);
			return true;
		}
		// The top, tipped toward the hole: four stops, dithered, by which side of the ring it is on and
		// by how steeply this one was tipped - so neighbours differ, and it reads as broken ground, not
		// as a laid wall. Soil, mostly: the lit earth is for the steepest slabs on the key side.
		float tilt = 0.35 + 0.65 * hash11(id + 5.0);
		float tone = 0.42 + 0.55 * lit * rise * tilt + (hash11(id + 37.0) - 0.5) * 0.3;
		float t = tone * 3.0 + (bayer4(cell) - 0.5) * 0.7;
		col = t > 2.35 ? EARTH_MID : (t > 1.6 ? EARTH_EDGE : (t > 0.85 ? SOIL_SHADE : SOIL_DEEP));
		// The slab's side that faces the key: one lit art pixel along the crack.
		vec2 tangent = vec2(-sin(a), cos(a));
		vec2 into = from0 < from1 ? tangent : -tangent;
		if (edge < GRID * (1.75 + 0.5 * rise) && dot(into, KEY) < 0.0)
			col = lit > -0.2 ? EARTH_HOT : EARTH_MID;
		// A bone in the earth, now and then.
		if (hash12(cell + id * 3.1) > 0.985)
			col = BONE_SHADE;
		return true;
	}
	// Past the slab, on the far side of the ring: the raised slab's shadow.
	if (lit < -0.2 && d < outer + GRID * (1.0 + 2.0 * rise * -lit))
		return true; // OCC

	// The cracks the breaking ran out along, from every other gap between the slabs.
	float gap = from0 < from1 ? slab : slab + 1.0;
	if (mod(gap, 2.0) > 0.5)
		return false;
	float gid = mod(gap, n);
	float run = rcur - outer;
	float reach = mod(gap, 4.0) < 0.5 ? rcur : outer + run * (0.35 + 0.35 * hash11(gid + 23.0));
	if (d > reach)
		return false;
	float along = clamp((d - outer) / max(GRID, run), 0.0, 1.0);
	float zig = sin(d * 0.27 + gid * 5.3) * 1.4;
	float lateral = (from0 < from1 ? from0 : -from1) - zig;
	float width = GRID * mix(1.25, 0.6, along);
	if (abs(lateral) < width)
		return true; // OCC
	vec2 across = vec2(-sin(a), cos(a)) * sign(lateral);
	if (abs(lateral) < width + GRID && dot(across, KEY) < 0.0) {
		col = along < 0.5 ? EARTH_MID : EARTH_EDGE;
		return true;
	}
	return false;
}

// One ring of small eyes, between `ring_in` and the edge of the slow, which they reach.
bool eye_ring(vec2 p, float a, float rcur, float ring_in, out vec3 col) {
	col = OCC;
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
	return eye(q, hw, wake * blink, gaze, col);
}

// Elder God Maw. A hole in the ground with a wet lip and the torn rim of the earth it broke through,
// three rings of teeth receding into the dark - each ring slower, smaller and dimmer than the one in
// front of it, turning against its neighbours - and a throat with something still moving at the
// bottom. Round the rim, a ring of small eyes that open one at a time once the maw is open, blink on
// their own clocks, and all look at the player.
bool maw(vec2 p, vec2 cell, float rcur, out vec3 col) {
	col = OCC;
	if (summon < BREACH)
		return heave(p, cell, rcur, summon / BREACH, col);
	float d = length(p);
	if (d > rcur)
		return false;
	float a = atan(p.y, p.x);
	vec2 dir = d > 0.01 ? p / d : vec2(0.0, 1.0);
	float lit = dot(dir, KEY);
	// Out of the dark: 0 at the breach, 1 once it has risen.
	float rise = smoothstep(BREACH, RISEN, summon);
	float breathe = sin(time * 1.6) * rise;
	float m = rcur * HOLE * mix(0.45, 1.0, rise) * (1.0 + 0.02 * breathe);

	if (d > m) {
		vec3 c;
		if (torn_rim(p, cell, d, a, lit, m, rcur, rise, c)) {
			col = c;
			return true;
		}
		float ring_in = rcur * (RIM + 0.02);
		if (d > ring_in && maw_age > 0.0 && eye_ring(p, a, rcur, ring_in, c)) {
			col = c;
			return true;
		}
		return false;
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
	// Rising, they are short and deep in the dark, and grow out of it into the light. Each ring's count
	// is from the full radius, so no tooth comes or goes while the maw opens or closes.
	for (int k = 0; k < 3; k++) {
		float fk = float(k);
		float ripple = 1.0 + 0.04 * sin(time * 1.6 - fk * 0.8);
		float rk = m * (0.95 - 0.22 * fk) * ripple;
		float lk = m * (0.17 - 0.035 * fk) * mix(0.3, 1.0, rise);
		float nk = max(8.0, floor(TAU * radius * HOLE * (0.95 - 0.22 * fk) / (16.0 - 2.0 * fk)));
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
			stop += (1.0 - rise) * 2.5;
			if (stop < 0.5) col = BONE_LIT;
			else if (stop < 1.5) col = BONE_BASE;
			else if (stop < 2.5) col = BONE_SHADE;
			else if (stop < 3.5) col = BONE_DEEP;
			else if (stop < 4.5) col = ROBE_BASE;
			else col = ROBE_SHADE;
			// Wet: a glint on the tip of a near tooth now and then, catching the light as it breathes.
			if (k == 0 && rise > 0.95 && lit > 0.25 && d < tip + GRID * 1.5 && hash12(cell + floor(time * 2.5)) > 0.75)
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
	// The breach: for a moment the dark under the ground is lit, and it boils up out of the hole.
	if (rise < 0.4 && hash12(cell + floor(time * 24.0)) > 0.88 + rise * 0.3)
		col = rise < 0.2 ? DARK_HOT : DARK_MID;
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
	} else {
		// The rift is the whole of the slow: its lip wanders inside the radius, never past it.
		hit = rift(p, cell, rcur, seed, col);
	}

	// Clods of earth thrown up while the ground is tearing open - inside the full radius only. Not the
	// maw's: what it throws is BlackTentacles', in the air and then on its rim.
	if (!hit && face != 1 && open_amount < 0.999) {
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
