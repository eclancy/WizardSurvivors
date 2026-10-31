// Thorned Vine (scripts/ThornVine.cs): the one green limb that bursts out of the ground, coils, cracks
// across the pack like a whip and withers back in - with the torn soil it came out of and the arc its tip
// cut - as pixel art on the game's 2 px grid.
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
// A WHIP, NOT A STICK (2026-10-31). "its animation just rotates, I want it to look more like the real arc
// of a whip." The vine used to be laid out here from five numbers (WhipLimb.Layout's integration), which
// could bend it but only ever turn it about its root. The node now lays it out as a whip stroke
// (WhipStroke) - the root leading, a loop rolling out along it, the tip trailing and then cracking - and
// hands this shader the lash itself: WhipStroke.Points points, root-local, root first, two to a texel of
// `lash`. A point's place along the vine is measured by how much vine lies between it and the root (the
// loop's points would otherwise take a third of the taper), so the thorns and leaves ride the loop round.
//
// DRAWN WHERE IT HURTS. The node tests its hits against WhipStroke.Front, the same curve these points
// sample, and the loop only ever turns over ground the front has already crossed.
//
// The arc the tip cut is the stormsweep rule: streaks over the ground the tip has just crossed, at the
// outer part of the vine's reach, densest right behind the tip and only between where the tip was a
// moment ago and where it is - so it is all but nothing while the tip trails, and a bright streak along
// the rim as it whips round and cracks. Speed, not a marker; never outside the vine's own reach.
//
// TWO PASSES, ONE MATERIAL. The vine and its arc are in the air (DrawLayers.Air), over the bodies it
// lashes; the torn soil it came out of is on the ground (DrawLayers.Ground), under them. The node draws
// one quad on each layer with this material, and the quad's colour says which: red 1 the air, 0 the ground.
shader_type canvas_item;
render_mode unshaded;

// The lash: PTS points, root-local, root first, two to a texel (xy, zw).
uniform sampler2D lash : filter_nearest, repeat_disable;
// Unit direction of the lash's first piece of stem out of the ground: nothing is drawn behind it.
uniform vec2 root_dir = vec2(1.0, 0.0);
// A snagged vine wraps what it caught (ThornVine, THE WRAP): the coil's end and the tail's start are not
// one piece of vine, and the segment between them (this index) is left out. Below 0, none.
uniform int break_at = -1;
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
// The arc the tip cut: x the angle the stroke started from, y the side it travels (+1/-1), z where the
// tip was a moment ago and w where it is now (radians from the start). smear_r is the tip's reach;
// smear_fade 1 .. 0 as it dies away.
uniform vec4 smear = vec4(0.0);
uniform float smear_r = 0.0;
uniform float smear_fade = 0.0;
// The torn soil at the root: 1 whole .. 0 gone; and the clods thrown while it grows (grow_u 0..1, or
// below 0 for none), out to clod_reach.
uniform float soil = 1.0;
uniform float grow_u = -1.0;
uniform float clod_reach = 30.0;

const int PTS = 31;
const int POINT_TEXELS = 16;
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
const vec3 GRASS_CORE = vec3(0.957, 1.0, 0.863);      // grass core #F4FFDC
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
	bool ground = COLOR.r < 0.5;
	vec2 cell = floor(world / GRID);
	vec2 p = (cell + 0.5) * GRID - origin;
	float b = bayer4(cell);

	vec3 col = OCC;
	bool hit = false;

	if (!ground) {
		// --- the lash, as the node laid it out ---------------------------------------------------
		vec2 pts[31];
		for (int x = 0; x < POINT_TEXELS; x++) {
			vec4 t = texelFetch(lash, ivec2(x, 0), 0);
			pts[2 * x] = t.xy;
			if (2 * x + 1 < PTS)
				pts[2 * x + 1] = t.zw;
		}

		// The nearest point of it: how far off, how many px from the root, its heading there, and which
		// side of it this cell is on.
		float best = 1e6;
		float best_run = 0.0;
		vec2 best_dir = root_dir;
		float best_side = 1.0;
		float run = 0.0;
		for (int j = 0; j < PTS - 1; j++) {
			if (j == break_at)
				continue;
			vec2 a = pts[j];
			vec2 ab = pts[j + 1] - a;
			float len2 = dot(ab, ab);
			float u = len2 > 0.0001 ? clamp(dot(p - a, ab) / len2, 0.0, 1.0) : 0.0;
			vec2 off = p - a - ab * u;
			float d = length(off);
			float seg = sqrt(len2);
			if (d < best && seg > 0.0001) {
				best = d;
				best_run = run + seg * u;
				best_dir = ab / seg;
				best_side = (ab.x * off.y - ab.y * off.x) >= 0.0 ? 1.0 : -1.0;
			}
			run += seg;
		}
		float total = run;
		// How far along the vine (0 root .. 1 tip), by the vine between it and the root.
		float best_t = best_run / max(total, 1.0);

		// Nothing of the stem behind the root: that half of it is still in the ground, so the vine comes
		// up out of its hole cut square rather than ending in a rounded club.
		bool alive = total >= GRID * 2.0 && dot(p, root_dir) >= -GRID * 0.5;

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
			// the stem, so as it grows the new ones come out with the tip, and round the loop they ride it.
			// Skipped near the root and the tip.
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

		// The arc the tip cut: streaks along its travel over the ground it has just crossed, in the outer
		// part of the reach, re-chosen every few frames - dense right behind the tip, thinning back toward
		// where it was a moment ago. While the tip trails it is a sliver; as it whips round and cracks it
		// is a bright streak along the rim.
		if (!hit && smear_fade > 0.0 && smear.w > smear.z) {
			float r = length(p);
			float q = smear.y * (mod(atan(p.y, p.x) - smear.x + PI, TAU) - PI);
			float span = smear.w - smear.z;
			if (q >= smear.z && q <= smear.w && r <= smear_r) {
				// Three speed lines one art pixel thick just inside the rim the tip ran along, the outer the
				// longest, each broken into dashes that are re-chosen every few frames, hottest at the tip.
				float g = (q - smear.z) / span;
				float flicker = floor(time / 0.05);
				for (int k = 0; k < 3; k++) {
					float rk = smear_r - GRID - float(k) * GRID * 2.5;
					if (abs(r - rk) > GRID * 0.5)
						continue;
					float reach_back = smear_fade * (1.0 - 0.3 * float(k));
					float dash = hash12(vec2(floor(q * rk / (GRID * 3.0)), float(k) * 7.0 + flicker));
					if (g >= 1.0 - reach_back && dash > 0.3) {
						col = g > 0.85 ? GRASS_CORE : (g > 0.55 ? GRASS_HOT : GRASS_MID);
						hit = true;
					}
				}
			}
		}
	} else if (soil > b) {
		// The soil it tore coming up: a hole, its lip lit toward the key and broken on the far side - and
		// while it grows, clods thrown out of it. It crumbles away cell by cell as the vine withers.
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
