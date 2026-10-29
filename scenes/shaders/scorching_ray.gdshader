// Scorching Ray (scripts/ScorchingRayBeam.cs): a thin fiery red laser splashing into the ground, as
// pixel art on the game's 2 px grid.
//
// It was two Line2Ds (a white core and a red glow, translucent, at whatever width the heat gave them)
// under nine antialiased draw calls - a translucent haze, pulses, a muzzle flare of soft circles and a
// spinning glint, a head of soft circles, a splash of squashed translucent discs, a stroked shockwave
// ring and smoke as faint discs - and Prism's split rays as three stroked lines each. At any angle but
// the four square ones that is the smooth vector look the Bonelight contract rules out
// (.ai/art-direction.md sections 1 and 3). So, as in riptide_wave.gdshader and void_lance.gdshader,
// every fragment snaps itself to the WORLD's 2 px cell first and only then measures that cell against
// the beam: the laser is a staircase of square art pixels at any angle, never a rotated stroke.
//
// Eric's two asks shape it: "a thin fiery red laser splashing into the ground" (2026-10-28) and "make
// the animation of scorching ray cooler". THIN: a white-hot core one art pixel wide (two at full heat)
// inside one pixel of ember-lit red and one of red, the red flickering a pixel wider here and there
// along it - about ten world px in all, the old line's width. RED: the core is Fire's white-hot and
// everything round it is the ember row (section 3's player pigment, "Pyromancer / Fireball"), never
// Fire's yellow mid, so the laser reads red rather than orange. SPLASHING: where it meets the ground a
// pool of molten light lies flat under the target's feet, white at its heart, and the node throws
// droplets and sparks out of it (as square cells, ScorchingRayBeam._Draw). COOLER: a white-hot head
// racing out ahead of the beam, a flare at the wizard as it fires, heat running down the beam in pulses
// while it holds, a white flash where it lands, smoke curling off the burn.
//
// Every colour is opaque: Fire's ramp, the ember row, Arcane's ramp for Prism's split rays, robe violet
// for soot. Fading is ordered dither: a part's "cover" says how much of it is left, and a cell is drawn
// only while its Bayer threshold is under it.
//
// THE BEAM IS THE HIT. The beam is drawn from the wizard to the ground under what it struck, where the
// damage lands. Where it burns more than that one body the band it burns is drawn too, out of the number
// the hit test uses: `fringe_half` is ScorchingRayBeam.PierceHalfWidth behind a target it burns through
// (embers drifting in the band, from where it struck to where the burning stops), and Sunlance's
// LanceHalfWidth x (1 + heat) while the lance sweeps (flames licking out across the band it burns, the
// furthest of them on its edge). Flashpoint's flashover is a blast whose front runs out to exactly the
// radius it hit (`flash_radius`), unsquashed, as the hit is. The burst an Area grows is ImpactDebris,
// thrown out to its radius by the node. Nothing else marks a reach (section 3, "no area markers").
//
// HOW IT IS DRAWN. Every quad the node lays down (one along the beam, one along each split ray, one over
// the strike, one over the wizard's hands) runs this same shader with the same uniforms, and every
// fragment shades the WHOLE ray at its cell, so where two quads overlap they paint the same pixel the
// same colour. The quads only say where to look. All lengths are world px; every point is a world
// (canvas) position, so the node itself never needs to turn.
shader_type canvas_item;
render_mode unshaded;

// The beam: from the wizard's hands to the ground under what it struck.
uniform vec2 beam_from = vec2(0.0);
uniform vec2 beam_to = vec2(0.0);
// How much of the beam is left (1 whole, 0 gone): it dissolves as it retracts.
uniform float beam_cover = 0.0;
// 0 a fresh target, 1 held to full heat (ScorchingRayBeam.Heat): thicker and whiter.
uniform float heat = 0.0;
// Sunlance: the lance's width over a ray's (1 for a ray).
uniform float lance = 1.0;
// While it holds: heat running down it from the hands to the target.
uniform bool pulses = false;
// While it extends: the white-hot head racing out at its tip.
uniform bool head = false;
uniform float time = 0.0;
uniform float seed = 0.0;
// The band it burns, along the beam from `fringe_from` px out: half `fringe_half` either side of it.
// Embers drifting for a beam that burns through; flames licking for Sunlance. `fringe_cover` 0 = none.
uniform float fringe_from = 0.0;
uniform float fringe_half = 0.0;
uniform float fringe_cover = 0.0;
uniform bool fringe_flames = false;
// The flare at the wizard's hands, 0..1.
uniform float muzzle = 0.0;
// The strike: where, how long ago (negative: not yet), how much of the pool is left, and the smoke.
uniform vec2 impact_at = vec2(0.0);
uniform float since = -1.0;
uniform float splash = 0.0;
uniform float smoke = 0.0;
// A held beam strikes bigger: 1 + half its heat.
uniform float grow = 1.0;
// Flashpoint's flashover: the radius its burst hit at full heat, 0 for none.
uniform float flash_radius = 0.0;
// Prism: where the ray broke, the ends of the split rays and how many there are.
uniform vec2 prism_from = vec2(0.0);
uniform vec2 prism_a = vec2(0.0);
uniform vec2 prism_b = vec2(0.0);
uniform vec2 prism_c = vec2(0.0);
uniform vec2 prism_d = vec2(0.0);
uniform int prism_count = 0;
uniform float prism_cover = 0.0;

const float GRID = 2.0;
const float HAND = 6.0;
const float FLASH_SECONDS = 0.14;
const float LAND_FLASH = 0.08;
const float SPLASH_SQUASH = 0.42;

const vec3 FIRE_CORE = vec3(1.000, 0.957, 0.816);    // fire core #FFF4D0
const vec3 FIRE_HOT = vec3(1.000, 0.784, 0.282);     // fire hot #FFC848
const vec3 EMBER_HI = vec3(1.000, 0.753, 0.659);     // ember hi #FFC0A8
const vec3 EMBER_LIT = vec3(1.000, 0.416, 0.290);    // ember lit #FF6A4A
const vec3 EMBER_BASE = vec3(0.847, 0.141, 0.180);   // ember base #D8242E
const vec3 EMBER_SHADE = vec3(0.541, 0.071, 0.125);  // ember shade #8A1220
const vec3 EMBER_DEEP = vec3(0.290, 0.031, 0.063);   // ember deep #4A0810
const vec3 ARC_CORE = vec3(1.000, 0.941, 1.000);     // arcane core #FFF0FF
const vec3 ARC_HOT = vec3(0.910, 0.753, 1.000);      // arcane hot #E8C0FF
const vec3 ARC_MID = vec3(0.753, 0.549, 1.000);      // arcane mid #C08CFF
const vec3 ARC_EDGE = vec3(0.478, 0.247, 0.831);     // arcane edge #7A3FD4
const vec3 VIOLET_SHADE = vec3(0.133, 0.110, 0.200); // robe violet shade #221C33
const vec3 VIOLET_DEEP = vec3(0.078, 0.063, 0.122);  // robe violet deep #14101F

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
	vec3 p3 = fract(vec3(p.xyx) * 0.1031 + seed * 0.0173);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

// Distance from p to the segment a-b, and how far along it (px from a) the nearest point is.
vec2 segment(vec2 p, vec2 a, vec2 b) {
	vec2 ab = b - a;
	float len2 = dot(ab, ab);
	float u = len2 > 0.0001 ? clamp(dot(p - a, ab) / len2, 0.0, 1.0) : 0.0;
	return vec2(length(p - a - ab * u), u * sqrt(len2));
}

// A thin ray of light: white core, a ring of `lit`, a flickering ring of `glow`, at distance d.
bool ray(float d, float along, float core, vec3 lit, vec3 glow, float flick_key, inout vec3 col) {
	float flick = hash12(vec2(floor(along / 6.0), flick_key));
	float red = GRID * (flick > 0.62 ? 2.0 : 1.0);
	if (d <= core) { col = FIRE_CORE; return true; }
	if (d <= core + GRID) { col = lit; return true; }
	if (d <= core + GRID + red) { col = glow; return true; }
	return false;
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 p = cell * GRID + GRID * 0.5;
	float b = bayer4(cell);
	float flicker = floor(time / 0.035);
	vec3 col = vec3(0.0);
	bool hit = false;

	// --- The flare at the wizard's hands: white-hot in the palm, a ring of hot light, red round it. ---
	if (muzzle > b) {
		float r = length(p - beam_from);
		float f = hash12(vec2(flicker, 3.0)) > 0.5 ? GRID : 0.0;
		if (r <= 2.2 * grow) { col = FIRE_CORE; hit = true; }
		else if (r <= 4.4 * grow) { col = muzzle > 0.6 ? FIRE_HOT : EMBER_LIT; hit = true; }
		else if (r <= 6.0 * grow + f) { col = EMBER_BASE; hit = true; }
	}

	// --- The strike: the flash the instant it lands, Flashpoint's flashover, the pool on the ground. ---
	if (!hit && since >= 0.0) {
		vec2 rel = p - impact_at;
		float r = length(rel);
		// The flash: a white disc that shrinks away in the first 80 ms.
		if (since < LAND_FLASH && r <= 16.0 * grow * (1.0 - since / LAND_FLASH)) {
			col = r > 12.0 * grow * (1.0 - since / LAND_FLASH) ? EMBER_HI : FIRE_CORE;
			hit = true;
		}
		// The flashover: a blast whose front runs out to exactly the radius it burned, white behind it
		// and dissolving through the dither as it goes. A circle, as the hit is.
		if (!hit && flash_radius > 0.0 && since < FLASH_SECONDS) {
			float k = since / FLASH_SECONDS;
			float front = flash_radius * (0.3 + 0.7 * (1.0 - (1.0 - k) * (1.0 - k)));
			if (r <= front && r <= flash_radius - GRID * 0.5) {
				if (r > front - GRID * 2.0) { col = k < 0.5 ? EMBER_HI : EMBER_LIT; hit = true; }
				else if ((1.0 - k) * 1.2 > b) { col = r < front * (0.6 - 0.4 * k) ? FIRE_CORE : FIRE_HOT; hit = true; }
			}
		}
		// The pool: molten light lying flat on the floor under the target's feet, white at its heart and
		// red at its ragged edge, which flickers.
		if (!hit && splash > b) {
			vec2 q = vec2(rel.x, rel.y / SPLASH_SQUASH);
			float rq = length(q);
			float rag = hash12(vec2(floor(atan(q.y, q.x) * 3.0), flicker)) > 0.55 ? GRID : 0.0;
			if (rq <= 4.0 * grow) { col = FIRE_CORE; hit = true; }
			else if (rq <= 8.5 * grow) { col = heat > 0.5 ? EMBER_HI : FIRE_HOT; hit = true; }
			else if (rq <= 13.0 * grow) { col = EMBER_LIT; hit = true; }
			else if (rq <= 18.0 * grow + rag) { col = EMBER_BASE; hit = true; }
			else if (rq <= 21.0 * grow + rag && b < 0.5) { col = EMBER_SHADE; hit = true; }
		}
	}

	// --- The head: a white-hot knot leading the beam out to its target. ---
	if (!hit && head) {
		float r = length(p - beam_to);
		float f = hash12(vec2(flicker, 5.0)) > 0.5 ? GRID : 0.0;
		if (r <= 2.6 * grow) { col = FIRE_CORE; hit = true; }
		else if (r <= 4.6 * grow) { col = FIRE_HOT; hit = true; }
		else if (r <= 6.0 * grow + f) { col = EMBER_LIT; hit = true; }
	}

	// --- The beam. ---
	if (!hit && beam_cover > b) {
		vec2 s = segment(p, beam_from, beam_to);
		float d = s.x;
		float along = s.y;
		float len = length(beam_to - beam_from);
		float core = (1.1 + 0.9 * heat) * lance;
		if (along >= HAND || len < HAND) {
			// Heat running down it from the hands to the target while it holds: a knot of white light
			// a pixel thicker than the beam, with the hot ring yellow round it.
			bool pulse = false;
			if (pulses && len > 24.0) {
				for (int i = 0; i < 3; i++) {
					float at = fract(time * 5.5 + float(i) / 3.0) * len;
					if (abs(along - at) < 9.0) pulse = true;
				}
			}
			float c = core + (pulse ? 1.0 : 0.0);
			if (ray(d, along, c, pulse ? FIRE_HOT : (heat > 0.6 ? EMBER_HI : EMBER_LIT), EMBER_BASE, flicker, col)) {
				hit = true;
			} else if (lance > 1.0 && d <= c + GRID * 2.0 + GRID * 2.5 * lance) {
				// A lance is the same fire, widened: round the laser a shimmer of heat - red thinning to
				// its shade through the dither, streaming out along it - never a solid sheath, which made
				// it the tube Eric did not want.
				float rr = (d - c - GRID * 2.0) / (GRID * 2.5 * lance);
				float stream = hash12(vec2(floor((along - time * 160.0) / 6.0), floor(d / GRID) + 3.0));
				if ((1.0 - rr) * 0.75 > b && stream > 0.25) {
					col = rr < 0.45 ? EMBER_BASE : EMBER_SHADE;
					hit = true;
				}
			} else if (d <= c + GRID * 4.0 && hash12(cell + flicker * 17.0) > 0.93) {
				// The crackle: sparks a pixel or two off the beam, a fresh few every flicker.
				col = hash12(cell + 3.0) > 0.5 ? EMBER_LIT : EMBER_BASE;
				hit = true;
			}
		}
		// The band it burns. Behind a target it burns through: embers drifting out to the allowance the
		// pierce test uses. Sunlance sweeping: flames licking out across the band, the furthest on its edge.
		if (!hit && fringe_cover > b && fringe_half > 0.0 && along >= fringe_from && d <= fringe_half && len > 1.0) {
			float k = 1.0 - d / fringe_half;
			vec2 dir = (beam_to - beam_from) / len;
			float side = dot(p - beam_from, vec2(-dir.y, dir.x)) >= 0.0 ? 1.0 : -1.0;
			if (fringe_flames) {
				// A tongue every 6 px along the lance, its own length each flicker, tapering to a point.
				float slot = floor(along / 6.0);
				float h = hash12(vec2(slot * 2.0 + side, floor(time / 0.06)));
				float reach = fringe_half * (h > 0.82 ? 1.0 : 0.25 + 0.6 * h);
				float u = d / reach;
				float mid = (slot + 0.5) * 6.0;
				if (d <= reach && abs(along - mid) <= 1.0 + 2.2 * (1.0 - u)) {
					col = u < 0.35 ? EMBER_LIT : (u < 0.7 ? EMBER_BASE : EMBER_SHADE);
					hit = true;
				}
			} else {
				vec2 drift = floor(vec2(along - time * 26.0, d * side - time * 7.0 * side) / GRID);
				float h = hash12(drift + vec2(side * 41.0, 0.0));
				if (h > 1.0 - 0.07 * (0.35 + 0.65 * k)) {
					col = h > 1.0 - 0.02 ? FIRE_HOT : (k > 0.5 ? EMBER_LIT : EMBER_BASE);
					hit = true;
				}
			}
		}
	}

	// --- Prism: the ray broken where it struck into thin rays with a violet edge, and the glint where
	// the light breaks. Under the strike's pool, over nothing else. ---
	if (!hit && prism_count > 0 && prism_cover > b) {
		vec2 ends[4] = vec2[4](prism_a, prism_b, prism_c, prism_d);
		float glint = length(p - prism_from);
		vec2 g = abs(p - prism_from);
		float arm = 7.0 + (hash12(vec2(flicker, 11.0)) > 0.5 ? GRID : 0.0);
		if (glint <= 3.0) { col = ARC_CORE; hit = true; }
		else if ((g.x <= 1.0 && g.y <= arm) || (g.y <= 1.0 && g.x <= arm)) { col = ARC_HOT; hit = true; }
		for (int i = 0; i < 4; i++) {
			if (hit || i >= prism_count)
				break;
			vec2 s = segment(p, prism_from, ends[i]);
			float r = length(p - ends[i]);
			if (r <= 2.2) { col = ARC_CORE; hit = true; }
			else if (r <= 4.0 + (hash12(vec2(flicker, float(i))) > 0.5 ? GRID : 0.0)) { col = ARC_MID; hit = true; }
			else if (ray(s.x, s.y, 1.0, ARC_HOT, ARC_MID, flicker + float(i) * 31.0, col)) { hit = true; }
			else if (s.x <= 6.0 && hash12(cell + flicker * 13.0) > 0.92) { col = ARC_EDGE; hit = true; }
		}
	}

	// --- Smoke off the burn: two curls of soot rising and widening, thinning through the dither. ---
	if (!hit && since >= 0.0 && smoke > 0.0) {
		for (int i = 0; i < 2; i++) {
			float clock = since * 1.6 - float(i) * 0.5;
			if (clock < 0.0)
				continue;
			float age = fract(clock);
			vec2 c = impact_at + vec2(sin(time * 3.0 + float(i) * 2.0) * 5.0, -8.0 - 34.0 * age);
			float r = (4.0 + 8.0 * age) * grow;
			float d = length(p - c);
			if (d <= r && (1.0 - age) * smoke * (1.0 - 0.5 * d / r) > b) {
				col = d < r * 0.5 ? VIOLET_SHADE : VIOLET_DEEP;
				hit = true;
				break;
			}
		}
	}

	if (!hit)
		discard;
	COLOR = vec4(col * COLOR.rgb, 1.0);
}
