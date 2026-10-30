// Arcane Explosion's blast (scripts/ArcaneExplosion.cs): a shockwave of arcane force thrown out from the
// wizard to exactly the radius it hits, as pixel art on the game's 2 px grid.
//
// It was tools/art/spell_fx.py's 32 px ring, saved at x2 and shown at range / 32 - x3.1 at the base
// reach, x7 at Supernova's - so its art pixels were 6 to 14 world px wide beside a wizard whose pixels
// are 2; a ring of 64 white GPU particles one world px wide grew under it; and every evolution's tint
// was multiplied over both. Here every fragment snaps to the WORLD's 2 px cell and is measured from the
// snapped centre, every colour is an opaque Arcane tone (or the tones an evolution says it is), and
// Bayer dithering is the only gradient.
//
// THE RADIUS IS THE HIT. `radius` is ArcaneExplosion's range, the distance TriggerExplosion tests, in
// WORLD px. A brief flash at the heart, then the front - a band of force, white-hot at its leading edge,
// cooling to the ramp's violet behind - leaves the wizard fast, coasts out and lands on the radius a
// quarter of a second in, throwing sparks of itself ahead that fall inside it; once it has landed it
// breaks up by dither and is gone. The one-shot shockwave of the effect's own light that
// .ai/art-direction.md section 3 names this spell as the example of - an animation, never a ring held.
// The middle clears behind the front, so the wizard is seen standing in his own spell.
//
// THE EVOLUTIONS ARE PAINTED, NOT TINTED: Dual Shockwave throws a second front after the first; Static
// Nova's front crackles with Lightning; Supernova's front burns, down the Fire ramp behind its white
// edge; Event Horizon's runs the other way - it starts on the radius and collapses into a dark heart,
// which is the pull it does. A Legendary's front is gold.
shader_type canvas_item;
render_mode unshaded;

uniform float radius = 100.0;
uniform float progress = 0.0;
uniform float seed = 0.0;
uniform bool dual = false;
uniform bool static_nova = false;
uniform bool supernova = false;
uniform bool event_horizon = false;
uniform bool gilded = false;

const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
const vec3 ARC_CORE = vec3(1.000, 0.941, 1.000);     // arcane core #FFF0FF
const vec3 ARC_HOT = vec3(0.910, 0.753, 1.000);      // arcane hot #E8C0FF
const vec3 ARC_MID = vec3(0.753, 0.549, 1.000);      // arcane mid #C08CFF
const vec3 ARC_EDGE = vec3(0.478, 0.247, 0.831);     // arcane edge #7A3FD4
const vec3 DARK_HOT = vec3(0.706, 0.549, 0.910);     // darkness hot #B48CE8
const vec3 DARK_MID = vec3(0.541, 0.361, 0.769);     // darkness mid #8A5CC4
const vec3 DARK_EDGE = vec3(0.208, 0.125, 0.361);    // darkness edge #35205C
const vec3 VIOLET_DEEP = vec3(0.078, 0.063, 0.122);  // robe violet deep #14101F
const vec3 FIRE_HOT = vec3(1.000, 0.784, 0.282);     // fire hot #FFC848
const vec3 FIRE_MID = vec3(0.910, 0.478, 0.102);     // fire mid #E87A1A
const vec3 FIRE_EDGE = vec3(0.722, 0.220, 0.102);    // fire edge #B8381A
const vec3 BOLT_CORE = vec3(1.000, 1.000, 0.941);    // lightning core #FFFFF0
const vec3 BOLT_MID = vec3(1.000, 0.941, 0.416);     // lightning mid #FFF06A
const vec3 BOLT_EDGE = vec3(0.878, 0.690, 0.165);    // lightning edge #E0B02A
const vec3 GOLD_HI = vec3(1.000, 0.941, 0.753);      // gold hi #FFF0C0
const vec3 GOLD_LIT = vec3(0.847, 0.690, 0.290);     // gold lit #D8B04A
const vec3 GOLD_BASE = vec3(0.541, 0.416, 0.118);    // gold base #8A6A1E

const float GRID = 2.0;
// The share of the blast the front takes to land on the radius.
const float ARRIVE = 0.6;

varying vec2 world;
varying flat vec2 origin;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	origin = MODEL_MATRIX[3].xy;
}

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031 + seed * 0.0173);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float hash11(float n) {
	return fract(sin(n * 12.9898 + seed * 7.8233) * 43758.5453);
}

float wrap_angle(float x) {
	return x - TAU * floor((x + PI) / TAU);
}

// Where a front is, 0..1 of the radius, k of the way through its run: fast out, coasting in.
float run(float k) {
	k = clamp(k, 0.0, 1.0);
	return 1.0 - (1.0 - k) * (1.0 - k) * (1.0 - k);
}

// The colour across a front: `x` 0 at its leading edge to 1 at its trailing one, `cool` 0 fresh to 1 spent.
vec3 band(float x, float cool, float b) {
	float lvl = x + cool * 0.45 + (b - 0.5) * 0.15;
	if (event_horizon)
		return lvl < 0.25 ? ARC_HOT : (lvl < 0.5 ? DARK_HOT : (lvl < 0.75 ? DARK_MID : DARK_EDGE));
	if (supernova)
		return lvl < 0.2 ? ARC_CORE : (lvl < 0.45 ? FIRE_HOT : (lvl < 0.7 ? FIRE_MID : FIRE_EDGE));
	if (gilded)
		return lvl < 0.2 ? ARC_CORE : (lvl < 0.45 ? GOLD_HI : (lvl < 0.7 ? GOLD_LIT : GOLD_BASE));
	return lvl < 0.2 ? ARC_CORE : (lvl < 0.45 ? ARC_HOT : (lvl < 0.7 ? ARC_MID : ARC_EDGE));
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 c0 = floor(origin / GRID + 0.5) * GRID;
	vec2 p = cell * GRID + GRID * 0.5 - c0;
	float d = length(p);
	float r = radius;
	if (d > r - GRID * 0.5)
		discard;
	float a = atan(p.y, p.x);
	float b = bayer4(cell);
	float t = progress;
	float flicker = floor(t * 24.0);
	float spent = clamp((t - ARRIVE) / (1.0 - ARRIVE), 0.0, 1.0);

	vec3 col = ARC_MID;
	bool hit = false;

	// The front. Nine lobes along it, only ever falling back from where it has got to - never ahead.
	float k = run(t / ARRIVE);
	float lead = event_horizon ? r * (1.0 - k * 0.92) : r * k;
	float lobe = 0.5 + 0.5 * sin(a * 9.0 + t * 4.0 + seed);
	float edge = lead - GRID * 2.5 * lobe;
	// Half a body's width or so: thicker on a wider blast, so a Supernova's front is still a wall of
	// force and not a hairline round the screen, and thinning as it coasts in. Drawn two art pixels wide
	// at the base reach, it read as an outline.
	float thick = clamp(r * 0.09, GRID * 3.0, 18.0) * mix(1.5, 0.9, k) * (supernova ? 1.2 : 1.0);
	if (event_horizon) {
		// Collapsing: the leading edge faces in, toward the dark heart it is falling into.
		float x = (d - (edge - GRID * 0.5)) / thick;
		if (x >= 0.0 && x < 1.0 && b > spent * 1.5) {
			col = band(x, k, b);
			hit = true;
		}
	} else {
		float x = (edge - d) / thick;
		if (x >= 0.0 && x < 1.0 && b > spent * 1.5) {
			col = band(x, k, b);
			hit = !(x > 0.7 && b > 0.6);
		}
	}

	// Dual Shockwave: a second front a beat behind the first, thinner, and landing on the radius too.
	if (dual && !event_horizon) {
		float k2 = run((t - 0.16) / ARRIVE);
		float lead2 = r * k2;
		float x2 = (lead2 - d) / (GRID * 2.0);
		if (k2 > 0.0 && x2 >= 0.0 && x2 < 1.0 && b > spent * 1.5 && b < 0.85) {
			col = band(0.25 + x2 * 0.5, k2, b);
			hit = true;
		}
	}

	// Static Nova: the front crackles. Short jagged arcs stand off it, re-chosen every flicker, in the
	// storm's own colours.
	if (static_nova && !hit && k > 0.05 && spent < 0.8) {
		float n = 14.0;
		float sector = floor((a / TAU + 0.5) * n);
		float h = hash11(sector + flicker * 13.0);
		if (h > 0.45) {
			float ac = (sector + 0.5) / n * TAU - PI + (h - 0.7) * 0.3;
			float zig = sin(d * 0.5 + h * 31.0 + flicker) * GRID * 1.2;
			float lateral = wrap_angle(a - ac) * d - zig;
			float len = GRID * (4.0 + 8.0 * hash11(sector + 3.1 + flicker));
			if (abs(lateral) < GRID * 0.75 && d < edge && d > edge - len) {
				col = (edge - d) < GRID * 2.0 ? BOLT_CORE : (h > 0.8 ? BOLT_MID : BOLT_EDGE);
				hit = true;
			}
		}
	}

	// Sparks of the force thrown out ahead of the front, which fall inside the radius: one art pixel,
	// white-hot, then cooling.
	if (!hit && !event_horizon && spent < 0.9) {
		float n = 18.0;
		float sector = floor((a / TAU + 0.5) * n);
		float h = hash11(sector * 1.37 + 0.5);
		float ac = (sector + 0.5 + (h - 0.5) * 0.6) / n * TAU - PI;
		float at = min(r - GRID, lead + GRID * (2.0 + 6.0 * h) * (1.0 - k * 0.6));
		vec2 sp = vec2(cos(ac), sin(ac)) * at;
		if (all(equal(floor((sp + c0) / GRID), cell)) && hash11(sector + 7.7) > 0.35) {
			col = t < 0.3 ? ARC_CORE : (t < 0.6 ? (supernova ? FIRE_HOT : ARC_HOT) : (supernova ? FIRE_MID : ARC_MID));
			if (static_nova && h > 0.5)
				col = BOLT_MID;
			hit = true;
		}
	}

	// Behind the front the middle clears, but a few motes of the force hang just inside it and wink out:
	// only just inside it, so the middle is clear and the motes are the front's wake, not a field of noise.
	if (!hit && !event_horizon && d < edge - thick && d > edge - thick * 3.0 && hash12(cell + flicker) < 0.05 * (1.0 - t)) {
		col = supernova ? FIRE_EDGE : ARC_EDGE;
		hit = true;
	}

	// The heart: a flash for the first instant, shrinking - or, for Event Horizon, the singularity the
	// front falls into, dark with a white rim, growing as the front closes on it.
	if (event_horizon) {
		// Small and dithered: it sits on the wizard's middle, and a solid dark disc there hid him for a
		// fifth of a second every blast.
		float heart = min(r * 0.08, 14.0) * run(t / ARRIVE) * (1.0 - spent * 0.7);
		if (d < heart && d >= heart - GRID) {
			col = ARC_CORE;
			hit = true;
		} else if (d < heart && b < 0.5) {
			col = b < 0.25 ? OCC : VIOLET_DEEP;
			hit = true;
		}
	} else {
		// Capped in world px: at Supernova's reach a flash a sixth of the radius was a white disc a
		// screen tall.
		float flash = clamp(1.0 - t / 0.18, 0.0, 1.0);
		float fr = min(r * 0.16, 22.0) * flash;
		if (d < fr) {
			col = ARC_CORE;
			hit = true;
		} else if (d < fr * 1.6 && b < 0.55 * flash) {
			col = supernova ? FIRE_HOT : ARC_HOT;
			hit = true;
		}
	}

	if (!hit)
		discard;
	COLOR = vec4(col, 1.0);
}
