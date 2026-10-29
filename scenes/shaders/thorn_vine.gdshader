// Thorned Vine (scripts/ThornVine.cs): the one green limb that bursts out of the ground, coils, lashes
// across the pack and withers back in - with the torn soil it came out of and the smear of its sweep -
// as pixel art on the game's 2 px grid.
//
// "Update all spells to the same quality we expect now" (2026-10-29). The vine was stamped square by
// square through WhipLimb, which is on the grid but coloured each square by LERPING bark into grass down
// its length and grass into a dry brown as it withered - dozens of in-between colours no ramp holds, which
// read as a smooth vector wedge - and its sweep was a dotted arc at 85% alpha. Here every fragment snaps
// to the WORLD's 2 px cell first and is then measured against the vine, as stormsweep_lash.gdshader does,
// and every colour is a stop of a Bonelight ramp: bark in the earth row and the gold row's soil, the
// living stem in Grass with verdant shade on its far side, poison at the tip. Bayer dithering is the only
// gradient - where the bark turns green, and where the vine dries - and nothing is translucent.
//
// DRAWN WHERE IT HURTS. The vine is laid out here by the same integration WhipLimb.Layout does, from the
// same five numbers ThornVine hands it (angle, vine_length, curl, wave_amp, wave_phase): the sweep is tested
// against WhipLimb's tip on the CPU, so the drawn tip and the struck enemy are the same point. Nothing
// is uploaded per frame but those scalars; the twelve segments are rebuilt per fragment.
//
// The sweep's smear is the stormsweep rule: streaks over the ground the vine has just crossed, densest
// right behind the lash and gone within a short angle of it - speed, not a marker - and never outside the
// vine's own reach. It replaced a dotted arc held at the tip's radius over the whole sweep, which under
// Hydra Vine's three heads closed into a dotted ring round the pack.
shader_type canvas_item;
render_mode unshaded;

// WhipLimb.Layout's numbers. The root is the node's origin.
uniform float angle = 0.0;
uniform float vine_length = 0.0;
uniform float curl = 0.0;
uniform float wave_amp = 0.0;
uniform float wave_phase = 0.0;
// The stamped width at the root, snapped to whole art pixels (WhipLimb.SnapSize); one art pixel at the tip.
uniform float base_width = 10.0;
// 0 living .. 1 dried out (the wither).
uniform float dry = 0.0;
// 1 while the vine is lashing or holding: the tip carries the poison, white-hot.
uniform float venom = 0.0;
// A legendary roll: the thorns are gold.
uniform bool gilded = false;
uniform float seed = 0.0;
uniform float time = 0.0;
// The sweep's smear: x the angle it started from, y the side it travels (+1/-1), z how far it has swept
// (radians), w the radius the tip sweeps at. smear_fade 1 .. 0 as it dies away.
uniform vec4 smear = vec4(0.0);
uniform float smear_fade = 0.0;
// The torn soil at the root: 1 whole .. 0 gone; and the clods thrown while it grows (grow_u 0..1, or
// below 0 for none), out to clod_reach.
uniform float soil = 1.0;
uniform float grow_u = -1.0;
uniform float clod_reach = 30.0;

const int SEGMENTS = 12;
const float GRID = 2.0;
const vec2 KEY = vec2(-0.70710678, -0.70710678);

const vec3 OCC = vec3(0.020, 0.027, 0.047);           // #05070C
// Bark and soil: the earth emissive row and the gold row's lower stops (gravewell_collapse's bank).
const vec3 EARTH_MID = vec3(0.788, 0.604, 0.369);     // earth mid #C99A5E
const vec3 EARTH_EDGE = vec3(0.478, 0.314, 0.141);    // earth edge #7A5024
const vec3 GOLD_SHADE = vec3(0.329, 0.247, 0.063);    // gold shade #543F10
const vec3 GOLD_DEEP = vec3(0.180, 0.133, 0.031);     // gold deep #2E2208
const vec3 GOLD_LIT = vec3(0.847, 0.690, 0.290);      // gold lit #D8B04A
// The living stem: the Grass ramp, and verdant shade for the side away from the key.
const vec3 GRASS_HOT = vec3(0.812, 0.933, 0.580);     // grass hot #CFEE94
const vec3 GRASS_MID = vec3(0.608, 0.831, 0.333);     // grass mid #9BD455
const vec3 GRASS_EDGE = vec3(0.290, 0.549, 0.227);    // grass edge #4A8C3A
const vec3 VERDANT_SHADE = vec3(0.145, 0.329, 0.086); // verdant shade #255416
// What it leaves in a wound.
const vec3 POISON_CORE = vec3(0.973, 1.0, 0.847);     // poison core #F8FFD8
const vec3 POISON_HOT = vec3(0.863, 0.957, 0.549);    // poison hot #DCF48C

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
	vec3 p3 = fract(vec3(p.xyx) * 0.1031 + seed * 0.0137);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

// A living tone and its dried one, chosen per cell: the wither eats the green cell by cell, never fades it.
vec3 withered(vec3 living, vec3 dried, float b) {
	return dry * 1.4 - 0.2 > b ? dried : living;
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 p = (cell + 0.5) * GRID - origin;
	float b = bayer4(cell);

	// --- the vine, laid out as WhipLimb.Layout lays it -------------------------------------------
	vec2 pts[13];
	pts[0] = vec2(0.0);
	float step_len = max(0.0, vine_length) / float(SEGMENTS);
	vec2 at = vec2(0.0);
	for (int j = 1; j <= SEGMENTS; j++) {
		float s = (float(j) - 0.5) / float(SEGMENTS);
		float theta = angle + curl * s * s + wave_amp * s * sin(wave_phase - s * 4.5);
		at += vec2(cos(theta), sin(theta)) * step_len;
		pts[j] = at;
	}

	// The nearest point of it: how far off, how far along (0 root .. 1 tip), how many px from the root,
	// its heading there, and which side of it this cell is on.
	float best = 1e6;
	float best_t = 0.0;
	float best_run = 0.0;
	vec2 best_dir = vec2(1.0, 0.0);
	float best_side = 1.0;
	float run = 0.0;
	for (int j = 0; j < SEGMENTS; j++) {
		vec2 a = pts[j];
		vec2 ab = pts[j + 1] - a;
		float len2 = dot(ab, ab);
		float u = len2 > 0.0001 ? clamp(dot(p - a, ab) / len2, 0.0, 1.0) : 0.0;
		vec2 off = p - a - ab * u;
		float d = length(off);
		float seg = sqrt(len2);
		if (d < best) {
			best = d;
			best_t = (float(j) + u) / float(SEGMENTS);
			best_run = run + seg * u;
			best_dir = seg > 0.0001 ? ab / seg : vec2(1.0, 0.0);
			best_side = (ab.x * off.y - ab.y * off.x) >= 0.0 ? 1.0 : -1.0;
		}
		run += seg;
	}
	float total = run;

	vec3 col = OCC;
	bool hit = false;
	// Nothing of the stem behind the root: that half of it is still in the ground, so the vine comes up
	// out of its hole cut square rather than ending in a rounded club.
	bool alive = vine_length >= GRID * 2.0 && dot(p, pts[1]) >= -GRID * 0.5;

	if (alive) {
		// Thick at the root, one art pixel at the tip (WhipLimb's straight wedge); its half-width.
		float w = max(GRID, floor(mix(base_width, GRID, best_t) / GRID + 0.5) * GRID) * 0.5;
		vec2 normal = vec2(-best_dir.y, best_dir.x) * best_side;
		float lit = dot(normal, KEY);
		float out_of = best - w;

		if (out_of <= 0.0) {
			// The body. Bark for the first third, greening through a dithered band into the living stem.
			bool green = (best_t - 0.28) / 0.14 > b;
			bool rim = out_of > -GRID;
			if (!green) {
				col = EARTH_EDGE;
				// Bark: a ring of dark grain every few px, broken, and the lit and shaded sides of the stem.
				if (mod(best_run + hash12(vec2(floor(best_run / 6.0), 3.0)) * 2.0, 6.0) < 1.6 && hash12(cell) > 0.35)
					col = GOLD_SHADE;
				if (rim)
					col = lit > 0.2 ? EARTH_MID : (lit < -0.2 ? GOLD_SHADE : col);
				col = withered(col, rim && lit < -0.2 ? GOLD_DEEP : GOLD_SHADE, b);
			} else {
				col = GRASS_EDGE;
				if (rim)
					col = lit > 0.2 ? GRASS_MID : (lit < -0.2 ? VERDANT_SHADE : col);
				// A crown of the lit side on the thick of the stem: a plane one pixel in from the rim.
				else if (lit > 0.2 && out_of > -GRID * 2.0 && w >= GRID * 2.5)
					col = GRASS_MID;
				col = withered(col, rim && lit > 0.2 ? EARTH_EDGE : (rim && lit < -0.2 ? GOLD_DEEP : GOLD_SHADE), b);
			}
			// The tip carries the poison while it strikes, white-hot at the very end.
			if (venom > 0.0 && best_run > total - GRID * 3.0 && dry < 0.05)
				col = best_run > total - GRID ? POISON_CORE : POISON_HOT;
			hit = true;
		} else if (out_of <= GRID) {
			// The outline: the contact shadow round the whole limb, so it holds on any floor and any body.
			col = OCC;
			hit = true;
		}

		// Thorns, raked back toward the root, alternating sides, one every 9 px along the stem - fixed to
		// the stem, so as it grows the new ones come out with the tip. Skipped near the root and the tip.
		if (!hit || out_of > 0.0) {
			float spacing = 9.0;
			float k = floor(best_run / spacing);
			float thorn_side = mod(k, 2.0) < 0.5 ? 1.0 : -1.0;
			float base_at = (k + 0.55) * spacing;
			float rise = out_of;
			float reach = 5.0;
			if (best_side == thorn_side && best_t > 0.1 && best_t < 0.9 && rise > -GRID && rise < reach + GRID) {
				float centre = base_at - max(rise, 0.0) * 0.65;
				float half_w = mix(2.4, 0.4, clamp(rise / reach, 0.0, 1.0));
				float da = abs(best_run - centre);
				if (rise <= reach && da <= half_w) {
					col = rise > reach * 0.55 ? (gilded ? GOLD_LIT : GRASS_HOT) : (gilded ? EARTH_MID : GRASS_MID);
					col = withered(col, EARTH_MID, b);
					hit = true;
				} else if (!hit && rise > 0.0 && da <= half_w + GRID) {
					col = OCC;
					hit = true;
				}
			}

			// Three leaves, each on its own short stalk, off the stem's sides.
			for (int l = 0; l < 3; l++) {
				float lt = 0.28 + float(l) * 0.22;
				float leaf_side = mod(float(l), 2.0) < 0.5 ? -1.0 : 1.0;
				if (best_side != leaf_side || abs(best_t - lt) > 0.12)
					continue;
				float leaf_at = lt * total;
				vec2 q = vec2(best_run - leaf_at, max(out_of, 0.0));
				// Leaning out toward the tip: along 4 px past its stalk, out 4 px from the stem.
				vec2 c = q - vec2(3.0 + q.y * 0.5, 4.0);
				float e = (c.x * c.x) / 20.0 + (c.y * c.y) / 6.5;
				if (out_of > 0.0 && e <= 1.0) {
					// The vein down its middle, the half toward the key lit, the far half in the stem's own green.
					bool vein = abs(c.y + c.x * 0.15) < 0.8 && abs(c.x) < 3.5;
					bool sunward = dot(normal, KEY) * (c.y < 0.0 ? -1.0 : 1.0) > -0.1;
					col = vein ? GRASS_EDGE : (sunward ? GRASS_MID : GRASS_EDGE);
					if (e > 0.55 && sunward && c.y > 0.0)
						col = GRASS_HOT;
					col = withered(col, e > 0.55 ? EARTH_EDGE : GOLD_SHADE, b);
					hit = true;
				} else if (!hit && out_of > 0.0 && (c.x * c.x) / 30.0 + (c.y * c.y) / 12.0 <= 1.0) {
					col = OCC;
					hit = true;
				}
			}
		}
	}

	// The smear of the sweep: the vine's own motion, streaks along the way it travels across the ground it
	// has just crossed, re-chosen every few frames - dense right behind the lash, gone within a short
	// angle of it, so it trails the vine and never stands round its reach as a ring (Hydra Vine's three
	// heads sweep the whole circle between them). Thicker toward the tip, which moves fastest.
	if (!hit && smear_fade > 0.0 && smear.z > 0.0) {
		float r = length(p);
		float q = smear.y * (mod(atan(p.y, p.x) - smear.x + PI, TAU) - PI);
		float behind = smear.z - q;
		float window = min(0.7, smear.z);
		if (q >= 0.0 && behind >= 0.0 && behind <= window && r <= smear.w && r > smear.w * 0.3) {
			float g = 1.0 - behind / window;
			float flicker = floor(time / 0.05);
			float streak = hash12(vec2(floor(r / GRID), flicker));
			float outer = (r - smear.w * 0.3) / (smear.w * 0.7);
			if (smear_fade * g * g * outer > b && streak > 0.45) {
				col = g > 0.85 && r > smear.w - 6.0 ? GRASS_HOT : (g > 0.5 ? GRASS_MID : GRASS_EDGE);
				hit = true;
			}
		}
	}

	// The soil it tore coming up: a hole, its lip lit toward the key and broken on the far side - and
	// while it grows, clods thrown out of it. It crumbles away cell by cell as the vine withers.
	if (!hit && soil > b) {
		float d = length(p);
		// A little wider than the stem, so the torn ground shows all round where it comes up.
		float ragged = base_width * 0.5 + 3.0 + 3.0 * hash12(vec2(floor(atan(p.y, p.x) * 2.5), 7.0));
		if (d < ragged) {
			if (d < ragged - GRID * 1.5)
				col = OCC;
			else
				col = dot(p, KEY) > 0.0 ? (hash12(cell) > 0.4 ? EARTH_MID : EARTH_EDGE) : (hash12(cell) > 0.5 ? GOLD_SHADE : GOLD_DEEP);
			hit = true;
		}
		if (!hit && grow_u >= 0.0) {
			for (int k = 0; k < 7; k++) {
				float a = seed + float(k) * 0.9;
				float dk = clod_reach * sqrt(grow_u) * (0.5 + 0.5 * sin(seed * 3.0 + float(k)));
				vec2 c = floor(vec2(cos(a), sin(a)) * dk / GRID) * GRID;
				vec2 rel = p - c;
				if (rel.x >= 0.0 && rel.x < GRID * 2.0 && rel.y >= 0.0 && rel.y < GRID * 2.0) {
					col = rel.y < GRID ? (mod(float(k), 2.0) < 0.5 ? EARTH_MID : EARTH_EDGE) : GOLD_SHADE;
					hit = true;
				}
			}
		}
	}

	if (!hit)
		discard;
	COLOR = vec4(col, 1.0);
}
