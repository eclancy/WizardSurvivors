// Lingering Rift's tear in the ground (ArcaneExplosion.OpenLingeringRift, drawn by
// scripts/BlastPoolSurface.cs): where an Arcane Explosion went off keeps hurting for a while. On the
// ground, under the bodies.
//
// It was a LingeringZone drawing itself - a translucent violet polygon, a darker one inside it and a soft
// bubble - which is a flat wash, the area marker the player's spells no longer carry. Here it is the
// thing that hurts, laid out to its radius (.ai/art-direction.md section 3, "a lasting zone"): a hole
// torn in the floor at the middle with arcane light lipping it, and the light wound out of it in arms
// that turn slowly toward it and run out exactly as far as the zone reaches, the floor showing between
// them. It flares on every bite - the zone's half-second tick - so the beat it hurts on is the beat it
// pulses on, and in its last moments it breaks up from the edge in, by dither.
//
// THE RADIUS IS THE HIT: LingeringZone.Radius, the reach its ticks test, and nothing is drawn past it;
// the arms' ragged ends fall short of it, never past. Every value is computed once per
// 2 px cell and every colour is an opaque Arcane, Darkness or violet tone; Bayer dithering is the only
// gradient.
//
// PER RIFT, IN THE VERTEX COLOUR (BlastPoolSurface): r how much of its life has gone (0..1), g the radius
// / 1024, b a seed, a its age in seconds / 4. The quad is its bounding square, UV 0..1.
shader_type canvas_item;
render_mode unshaded;

const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
const vec3 ARC_CORE = vec3(1.000, 0.941, 1.000);     // arcane core #FFF0FF
const vec3 ARC_HOT = vec3(0.910, 0.753, 1.000);      // arcane hot #E8C0FF
const vec3 ARC_MID = vec3(0.753, 0.549, 1.000);      // arcane mid #C08CFF
const vec3 ARC_EDGE = vec3(0.478, 0.247, 0.831);     // arcane edge #7A3FD4
const vec3 DARK_EDGE = vec3(0.208, 0.125, 0.361);    // darkness edge #35205C
const vec3 VIOLET_DEEP = vec3(0.078, 0.063, 0.122);  // robe violet deep #14101F

const float GRID = 2.0;
const float SEEP_SECONDS = 0.2;
// LingeringZone's tick for the rift (ArcaneExplosion.OpenLingeringRift).
const float TICK_SECONDS = 0.5;

varying vec2 local;
varying flat vec4 params;

void vertex() {
	params = COLOR;
	float r = COLOR.g * 1024.0;
	local = (UV - vec2(0.5)) * 2.0 * vec2(r);
}

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

float hash12(vec2 p, float s) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031 + s * 0.0137);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float wobble(float a, float s) {
	return sin(a * 3.0 + s) * 0.55 + sin(a * 5.0 - s * 1.7) * 0.3 + sin(a * 9.0 + s * 0.4) * 0.15;
}

void fragment() {
	float life = params.r;
	float radius = params.g * 1024.0;
	float s = params.b * 97.0;
	float age = params.a * 4.0;

	vec2 cell = floor(local / GRID);
	vec2 p = cell * GRID + GRID * 0.5;
	float d = length(p);
	float a = atan(p.y, p.x);
	float seep = mix(0.35, 1.0, clamp(age / SEEP_SECONDS, 0.0, 1.0));
	float reach = (radius - GRID) * seep;
	if (d > reach || radius < GRID * 4.0)
		discard;
	float u = d / reach;
	float b = bayer4(cell);

	float gone = clamp((life - 0.85) / 0.15, 0.0, 1.0);
	if (b < gone * 1.3 - (1.0 - u) * 0.3)
		discard;

	// The bite: a flare on every tick, fading over the next third of a second. Past four seconds the age
	// stops being carried, and the flare keeps its own beat.
	float since = age < 3.99 ? mod(age, TICK_SECONDS) : mod(TIME, TICK_SECONDS);
	float flare = 1.0 - clamp(since / 0.3, 0.0, 1.0);

	// The hole at the middle: a darkness the floor has fallen into, its lip lit from below.
	float hole = clamp(radius * 0.1, GRID * 3.0, 20.0) * (1.0 + 0.12 * wobble(a, s + 2.0));
	vec3 col;
	bool hit = false;
	if (d < hole) {
		col = d < hole * 0.55 ? OCC : (b < 0.5 ? VIOLET_DEEP : OCC);
		// A glint deep in it, now and then.
		if (hash12(cell, s + floor(TIME * 6.0)) > 0.97)
			col = DARK_EDGE;
		hit = true;
	} else if (d < hole + GRID * 2.0) {
		col = d < hole + GRID ? (flare > 0.5 ? ARC_CORE : ARC_HOT) : ARC_MID;
		hit = true;
	} else {
		// The arms: light wound out of the hole, turning slowly in toward it, thinning as they go and
		// running out to the reach. Their width is in world px - a hand's breadth at the hole, a couple of
		// art pixels at the reach - so a Supernova-sized rift is longer arms, not fatter ones, and the floor
		// shows between them.
		float arms = clamp(floor(radius / 40.0), 3.0, 5.0);
		float twist = 1.4;
		float w = a / TAU * arms + u * twist + TIME * 0.35 + s;
		float arm = abs(fract(w) - 0.5) * 2.0;
		float period = TAU * max(d, 1.0) / arms;
		float width = clamp(mix(12.0, 4.0, u) * (1.0 + 0.5 * flare) / period, 0.02, 0.45);
		// Ragged: each arm's end comes in a little short of the reach, by its own amount.
		float end = 1.0 - 0.1 * hash12(vec2(floor(w), 3.0), s);
		if (1.0 - arm < width && u < end) {
			float across = (1.0 - arm) / width;
			float lvl = 0.2 + u * 0.6 + across * 0.3 - flare * 0.4 + (b - 0.5) * 0.18;
			col = lvl < 0.3 ? ARC_HOT : (lvl < 0.55 ? ARC_MID : (lvl < 0.85 ? ARC_EDGE : DARK_EDGE));
			hit = true;
			// The arm's sides break up into dither, more of them further out, so it is light on the floor
			// rather than a band of paint; its spine runs on to the end.
			if (across > 0.35 && b > 1.15 - u * 0.5)
				hit = false;
		} else if (1.0 - arm < width * 2.5 && hash12(cell, s + 5.0) < 0.05 * (1.0 - u) + 0.05 * flare) {
			// Motes of the light shed beside the arms, brighter on a bite.
			col = hash12(cell, s + floor(TIME * 4.0)) > 0.5 ? ARC_MID : ARC_EDGE;
			hit = true;
		}
		// No lip at the reach: a ring of flakes there read as a dotted outline (crater.gdshader's lesson).
		// The arms running out to it are what say how far it goes.
	}
	if (!hit)
		discard;
	COLOR = vec4(col, 1.0);
}
