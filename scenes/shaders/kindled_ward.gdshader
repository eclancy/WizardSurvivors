// Kindled Ward (scripts/KindledWard.cs): a mote of made light that drifts at the nearest enemy and burns
// it, as pixel art on the game's 2 px grid.
//
// It was three translucent DrawCircles (a halo at 22% over a body at 55% over a core) with a fainter
// fourth for a grown ward, and every strike's flare a fan of DrawLines at fractional widths and alpha:
// soft circles and washes in a hard-edged, nearest-filtered game (.ai/art-direction.md sections 1 and 3).
// Here every fragment snaps to the WORLD's 2 px cell first and is measured from the ward's centre, so
// every part is square art pixels: one quad per ward, opaque colours from the ward's ramp (Light's, or the
// element an element path turns it to - Rimelight's Ice), Bayer dithering the only gradient.
//
// WHAT IT IS. A made light, not a creature (.ai/world-and-tone.md: no face, no allies): a white-hot heart
// in a flame of the ramp's hot and mid that licks upward and flickers, and round it a glitter of its
// light - a few cells, never a disc. A grown ward (its burn reaches further: Area, Pyre's small sun) is
// drawn bigger in proportion (KindledWard.DrawScale) and throws short rays of light, a corona, so a sun
// reads as a light source at a glance.
//
// THE FLARE IS THE REACH. Nothing marks how far it burns while it waits (the old halo as wide as the
// strike was a marker, and went in 2026-10-23). Every strike, tongues of its light lick out to exactly
// `flare_radius` - KindledWard.StrikeRadius, the reach the strike just tested - and draw back in; twice as
// many and solid when it caught something, half as many and broken when it did not. Flare's burst
// (`big_radius`, StrikeRadius x FlareReachScale) does the same out to the reach it hit, with ImpactDebris
// thrown to it by the node. The corona is held well inside the reach.
//
// GUTTERING. In its last second the ward dissolves through the dither from the outside in (`fade`), the
// one warning the player gets to cast the next.
shader_type canvas_item;
render_mode unshaded;

uniform vec4 tone_core : source_color = vec4(1.0, 1.0, 1.0, 1.0);        // light core #FFFFFF
uniform vec4 tone_hot : source_color = vec4(1.0, 0.973, 0.847, 1.0);     // light hot #FFF8D8
uniform vec4 tone_mid : source_color = vec4(1.0, 0.941, 0.659, 1.0);     // light mid #FFF0A8
uniform vec4 tone_edge : source_color = vec4(1.0, 0.776, 0.235, 1.0);    // light edge #FFC63C
// How much bigger than a fresh ward it is drawn (KindledWard.DrawScale).
uniform float grown = 1.0;
// 1, falling toward 0 in its last second.
uniform float fade = 1.0;
uniform float time = 0.0;
uniform float seed = 0.0;
// The strike's flare: how far through it (0..1; 1 or more is none), the reach, whether it caught
// anything, how far the tongues are turned this strike, and how many there are.
uniform float flare_t = 1.0;
uniform float flare_radius = 34.0;
uniform bool flare_hit = false;
uniform float flare_turn = 0.0;
uniform float flare_tongues = 8.0;
// Flare's burst, once a ward: the same, out to its own reach.
uniform float big_t = 1.0;
uniform float big_radius = 0.0;
uniform float big_tongues = 16.0;
// The corona's reach, held inside the strike's.
uniform float corona = 0.0;

const float GRID = 2.0;
const float HALO = 13.0;
const float INNER = 6.0;

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

// Tongues of light licking out from the ward to `radius`, `t` of the way through: out fast, then burning
// away from the root outward, so the tips are the last of it - the reach is what stays in the eye. Every
// other one short, so it reads as flame and not as a wheel; each tapers from its root to a pixel at its
// tip, and there are never so many that they run together into a disc (KindledWard caps the count by the
// gap between them). The tips stop a cell inside the radius.
bool flare(vec2 p, float r, float t, float radius, float tongues, float turn, bool solid, inout vec3 col) {
	float out_t = min(1.0, t / 0.4);
	float reach = 1.0 - (1.0 - out_t) * (1.0 - out_t);
	float gone = smoothstep(0.4, 1.0, t);
	float step = TAU / tongues;
	float a = atan(p.y, p.x) - turn;
	float i = floor(a / step + 0.5);
	float off = abs(a - i * step) * r;
	bool even = mod(i, 2.0) < 0.5;
	float len = (even ? 1.0 : 0.7) * (radius - GRID) * reach;
	float root = max(INNER, len * gone);
	if (r < root || r > len)
		return false;
	float u = r / max(len, 1.0);
	float base_w = max(GRID * 0.5, mix(3.0, 1.0, t) * grown * 0.5);
	if (off > max(GRID * 0.5, base_w * (1.0 - 0.75 * u)))
		return false;
	col = u > 0.85 ? tone_edge.rgb : (even ? tone_hot.rgb : tone_mid.rgb);
	if (u < 0.3)
		col = tone_core.rgb;
	// Solid when it caught something; broken, every other cell, when it burned the air.
	return solid || mod(floor(r / GRID), 2.0) < 0.5;
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 p = cell * GRID + GRID * 0.5 - origin;
	float r = length(p);
	float b = bayer4(cell);
	float flick = floor(time / 0.06);
	float s = grown * (1.0 + 0.08 * sin(time * 9.3));
	vec3 col = vec3(0.0);
	bool hit = false;
	// What the guttering keeps: the heart longest, the glitter first.
	float keep = fade * 1.6 - r / (HALO * s) * 0.6;

	// The flame: a teardrop of light standing up from its heart, taller than wide, its tip licking.
	vec2 q = vec2(p.x, p.y < 0.0 ? p.y * 0.62 : p.y * 1.1);
	float lick = hash12(vec2(floor(p.x / GRID), flick)) > 0.55 ? GRID : 0.0;
	float fr = length(q);
	if (fr <= 2.6 * s) { col = tone_core.rgb; hit = true; }
	else if (fr <= 5.0 * s) { col = tone_hot.rgb; hit = true; }
	else if (fr <= 7.6 * s + (p.y < 0.0 ? lick : 0.0)) { col = tone_mid.rgb; hit = true; }
	else if (fr <= 9.0 * s && p.y < -2.0 * s && b < 0.5) { col = tone_edge.rgb; hit = true; }

	// The glitter round it: a few cells of its light, twinkling, never a ring or a disc.
	if (!hit && r > 8.0 * s && r <= HALO * s) {
		float h = hash12(cell + flick * 7.0);
		if (h > 0.9) { col = h > 0.97 ? tone_hot.rgb : tone_edge.rgb; hit = true; }
	}

	// A grown ward's corona: short rays of its light, turning slowly, flickering in length.
	if (!hit && corona > HALO * s * 0.8 && r > 8.0 * s && r <= corona) {
		float rays = 12.0;
		float a = atan(p.y, p.x) - time * 0.6;
		float i = floor(a / (TAU / rays) + 0.5);
		float off = abs(a - i * TAU / rays) * r;
		float len = corona * (mod(i, 2.0) < 0.5 ? 1.0 : 0.65) * (0.8 + 0.2 * hash12(vec2(i, flick)));
		if (r <= len && off <= GRID * 0.75) {
			float u = (r - 8.0 * s) / max(1.0, len - 8.0 * s);
			col = u < 0.4 ? tone_mid.rgb : tone_edge.rgb;
			hit = u < 0.6 || b < 0.6;
		}
	}

	if (hit && keep < b)
		hit = false;

	// The strike's flare, and Flare's burst: out to exactly the reach each tested. A guttering ward's
	// flare thins through the dither with it.
	float gutter = fade * 1.6 - 0.4;
	if (!hit && flare_t < 1.0 && gutter > b) {
		if (flare(p, r, flare_t, flare_radius, flare_tongues, flare_turn, flare_hit, col))
			hit = true;
	}
	if (!hit && big_t < 1.0 && big_radius > 0.0 && gutter > b) {
		if (flare(p, r, big_t, big_radius, big_tongues, flare_turn + 0.2, true, col))
			hit = true;
	}

	if (!hit)
		discard;
	// A Legendary roll keeps its gold (the node's modulate, Player.FireKindledWard).
	COLOR = vec4(col * COLOR.rgb, 1.0);
}
