// A crater in the ground: the hole a meteor punches (Meteor Swarm, scripts/MeteorImpact.cs through
// CraterLayer) and the burning hole under each of Cataclysm's craters (CataclysmField). One quad per
// crater, drawn by CraterGround.Draw at absolute z -15 - over the terrain (-100) and the walkable
// decals (-20), under every actor (0) - because a crater lies ON the ground and must never cover the
// wizard standing in it (2026-10-28: "the crater from meteor should not cover other elements that would
// stand on top of the ground, like the player").
//
// What it draws, from the middle out: a scorched floor that is molten when the crater opens and crusts
// over as it cools, the crust's seams glowing last; the inner wall, lit on the far side from the key
// and in shadow on the near one, as a bowl is; a raised, lumpy rim whose crest catches the light, with
// the bank falling away outside it and an occlusion shadow at its foot on the side away from the key;
// cracks running out from the floor through the rim, glowing while it is hot; and the ejecta - soot
// thrown out in rays and rocks scattered among them, some still glowing - stopping short of the hit
// radius. At the moment of the hit the bowl flashes white and the light runs out over the ground to
// the radius and goes out there (an animation, .ai/art-direction.md section 3, not a marker).
//
// Drawn here rather than as a sheet for gravewell_collapse.gdshader's reason: the radius is the hit
// radius, which Area scales continuously, so no sheet at an integer scale could match it, and stamping
// it square by square would be thousands of rects. Every value is computed once per 2 px cell (the
// game's art pixel) and every colour is a Bonelight tone: occlusion, the earth and fire emissive ramps,
// the gold row's shade and deep that the soil is made of (Gravewell's bank, for the same reason - a bank
// in the stone row vanished on the stone floors), the red row for char and cooling coals, violet deep
// for soot. Gradients are ordered dither; nothing is blended and nothing is antialiased.
//
// PER CRATER, IN THE VERTEX COLOUR, so every crater on screen shares one material and one batch:
//   r  heat - 1 white-hot, 0 cold. CraterGround.HeatAt is the curve; the flicker is in it.
//   g  seconds since it opened, / 4 (only the first fifth of a second is read, for the flash).
//   b  the radius the hit tested, / 1024. NOTHING IS DRAWN PAST IT.
//   a  how much of the crater is left: 1 whole, 0 gone. It dissolves from the outside in.
// The quad is the crater's bounding box, radius wide and radius * SQUASH tall either side of its
// centre, and its UV runs 0..1 across it; CraterGround.Draw and this file share SQUASH.
shader_type canvas_item;
render_mode unshaded;

const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
const vec3 EARTH_HOT = vec3(0.910, 0.769, 0.549);    // earth hot #E8C48C
const vec3 EARTH_MID = vec3(0.788, 0.604, 0.369);    // earth mid #C99A5E
const vec3 EARTH_EDGE = vec3(0.478, 0.314, 0.141);   // earth edge #7A5024
const vec3 GOLD_SHADE = vec3(0.329, 0.247, 0.063);   // gold shade #543F10
const vec3 GOLD_DEEP = vec3(0.180, 0.133, 0.031);    // gold deep #2E2208
const vec3 RED_LIT = vec3(0.416, 0.125, 0.157);      // red lit #6A2028
const vec3 RED_BASE = vec3(0.290, 0.094, 0.125);     // red base #4A1820
const vec3 RED_SHADE = vec3(0.165, 0.055, 0.078);    // red shade #2A0E14
const vec3 RED_DEEP = vec3(0.086, 0.027, 0.039);     // red deep #16070A
const vec3 VIOLET_DEEP = vec3(0.078, 0.063, 0.122);  // violet deep #14101F
const vec3 FIRE_CORE = vec3(1.000, 0.957, 0.816);    // fire core #FFF4D0
const vec3 FIRE_HOT = vec3(1.000, 0.784, 0.282);     // fire hot #FFC848
const vec3 FIRE_MID = vec3(0.910, 0.478, 0.102);     // fire mid #E87A1A
const vec3 FIRE_EDGE = vec3(0.722, 0.220, 0.102);    // fire edge #B8381A

const float GRID = 2.0;
// The bowl is round on the ground and seen from the game's three-quarter view: squashed in y.
// CraterGround.Squash is the same number.
const float SQUASH = 0.84;
// The rim's radius as a share of the hit radius. The ejecta fills the rest, out to just short of it.
const float RIM_SHARE = 0.55;
// The key light, from the upper left (.ai/art-direction.md section 2).
const vec2 KEY = vec2(-0.70710678, -0.70710678);
const float FLASH_SECONDS = 0.18;

varying vec2 local;
varying flat vec4 params;
varying flat vec2 centre;

void vertex() {
	params = COLOR;
	float r = COLOR.b * 1024.0;
	local = (UV - vec2(0.5)) * 2.0 * vec2(r, r * SQUASH);
	centre = VERTEX - local;
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

vec2 hash22(vec2 p, float s) {
	vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973) + s * 0.0137);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.xx + p3.yz) * p3.zy);
}

float hash11(float n, float s) {
	return fract(sin(n * 12.9898 + s * 7.8233) * 43758.5453);
}

// Three harmonics: one frequency reads as a wobble, three as a shape.
float wobble(float a, float s) {
	return sin(a * 3.0 + s) * 0.55 + sin(a * 5.0 - s * 1.7) * 0.3 + sin(a * 9.0 + s * 0.4) * 0.15;
}

// The distance to the nearest crust seam, in cells: Voronoi F2 - F1.
float seam(vec2 x, float s) {
	vec2 n = floor(x);
	vec2 f = fract(x);
	float f1 = 8.0;
	float f2 = 8.0;
	for (int j = -1; j <= 1; j++) {
		for (int i = -1; i <= 1; i++) {
			vec2 g = vec2(float(i), float(j));
			vec2 r = g + hash22(n + g, s) - f;
			float dd = dot(r, r);
			if (dd < f1) {
				f2 = f1;
				f1 = dd;
			} else if (dd < f2) {
				f2 = dd;
			}
		}
	}
	return sqrt(f2) - sqrt(f1);
}

// Soil by how much key light it faces: -1 turned away, 1 square to it.
vec3 soil(float lit) {
	if (lit > 0.6)
		return EARTH_MID;
	if (lit > 0.18)
		return EARTH_EDGE;
	if (lit > -0.25)
		return GOLD_SHADE;
	if (lit > -0.65)
		return GOLD_DEEP;
	return RED_DEEP;
}

// Glow by emission: white-hot to a dull coal. Below the last band it is not glowing at all.
bool glow(float e, out vec3 col) {
	col = FIRE_EDGE;
	if (e > 0.92) col = FIRE_CORE;
	else if (e > 0.72) col = FIRE_HOT;
	else if (e > 0.52) col = FIRE_MID;
	else if (e > 0.34) col = FIRE_EDGE;
	else if (e > 0.22) col = RED_LIT;
	else if (e > 0.13) col = RED_BASE;
	else return false;
	return true;
}

float wrap_angle(float x) {
	return x - TAU * floor((x + PI) / TAU);
}

void fragment() {
	float heat = params.r;
	float age = params.g * 4.0;
	float radius = params.b * 1024.0;
	float fade = params.a;

	vec2 cell = floor(local / GRID);
	vec2 p = cell * GRID + GRID * 0.5;
	vec2 q = vec2(p.x, p.y / SQUASH);
	float d = length(q);
	if (d > radius - GRID || radius < GRID * 4.0)
		discard;

	// Dissolving: the outside goes first, so a dying crater shrinks into its own bowl.
	if (bayer4(cell + vec2(1.0, 2.0)) > fade * 1.35 - (d / radius) * 0.35)
		discard;

	float s = hash12(mod(floor(centre / GRID), 4096.0), 0.0) * 97.0;
	float a = atan(q.y, q.x);
	vec2 dir = d > 0.01 ? q / d : vec2(0.0, 1.0);
	float lit_out = dot(dir, KEY);
	float dith = bayer4(cell);
	float grain = hash12(cell, s);

	float rim = radius * RIM_SHARE * (1.0 + 0.07 * wobble(a, s));
	float u = d / rim;
	float bank = 1.34 + 0.12 * wobble(a * 1.3 + 1.7, s * 3.1);

	vec3 col = OCC;
	bool drawn = true;
	float e = 0.0;

	if (u < 0.5) {
		// The floor: char, a red-black speckled with the soil's deep.
		col = grain > 0.72 ? GOLD_DEEP : (grain > 0.3 ? RED_DEEP : RED_SHADE);
	} else if (u < 0.92) {
		// The inner wall: lit on the far side from the key, in shadow on the near side; charred low down.
		float w = (u - 0.5) / 0.42;
		float lit = -lit_out * (0.45 + 0.55 * w) + (dith - 0.5) * 0.35;
		col = soil(lit);
		if (dith > w * 2.6 + 0.35)
			col = grain > 0.5 ? RED_DEEP : RED_SHADE;
	} else if (u < 1.06) {
		// The crest: the highest ground, so the key side catches the light hardest.
		float lit = lit_out * 0.8 + 0.25 + (grain - 0.5) * 0.3;
		col = lit > 0.85 ? EARTH_HOT : soil(lit);
	} else if (u < bank) {
		// The outer bank, falling away from the crest: thrown-up rubble in clods, lit from the key side.
		float w = (u - 1.06) / max(0.01, bank - 1.06);
		vec2 clump = floor(cell / 2.0);
		float k = hash12(clump, s + 3.0);
		float lit = lit_out * (0.9 - 0.5 * w) + (k - 0.5) * 0.7 - w * 0.25;
		col = soil(lit);
		// The foot of the bank breaks up onto the ground rather than stopping on a line.
		if (w > 0.6 && hash12(clump, s + 5.0) < (w - 0.6) * 2.4)
			drawn = false;
	} else {
		drawn = false;
		float out_d = (u - bank) * rim;
		float span = max(GRID, radius - bank * rim);
		float far = clamp(out_d / span, 0.0, 1.0);

		// Occlusion at the foot of the bank, on the side away from the key: the rim's cast shadow.
		float shadow = bank + 0.1 + 0.14 * (-lit_out);
		if (lit_out < -0.15 && u < shadow && hash12(cell, s + 53.0) < 0.85 - 0.6 * (u - bank) / (shadow - bank)) {
			col = OCC;
			drawn = true;
		}

		// Soot thrown out in rays, and a scorch round the rim that thins with distance. In flakes of an
		// art pixel scattered by hash, not an ordered dither: a halftone of black round the bank read as
		// a dotted outline.
		float nr = clamp(floor(radius / 9.0), 7.0, 15.0);
		float rs = floor(((a + 0.31) / TAU + 0.5) * nr);
		float rh = hash11(rs * 3.7 + 0.5, s);
		float rc = (rs + 0.5 + (rh - 0.5) * 0.6) / nr * TAU - PI - 0.31;
		float roff = wrap_angle(a - rc);
		float rlen = radius * (0.55 + 0.38 * hash11(rs + 4.4, s));
		float rwidth = mix(rim * 0.38, GRID, clamp((d - bank * rim) / max(GRID, rlen - bank * rim), 0.0, 1.0));
		bool ray = d < rlen && abs(roff * d) < rwidth;
		float soot = ray ? mix(0.55, 0.08, clamp((d - bank * rim) / max(GRID, rlen - bank * rim), 0.0, 1.0))
				: mix(0.35, 0.0, clamp(far * 3.2, 0.0, 1.0));
		if (!drawn && hash12(cell, s + 41.0) < soot) {
			col = grain > 0.55 ? VIOLET_DEEP : RED_DEEP;
			drawn = true;
		}

		// Rocks: clods of two art pixels thrown among the soot, thicker in the rays, a few still glowing.
		vec2 clump = floor(cell / 2.0);
		vec2 corner = cell - clump * 2.0;
		float k = hash12(clump, s + 11.0);
		float rock = mix(0.3, 0.04, far) * (ray ? 1.7 : 1.0);
		vec2 clump_centre = (clump * 2.0 + 1.0) * GRID;
		if (k < rock && length(vec2(clump_centre.x, clump_centre.y / SQUASH)) < radius - GRID * 2.0) {
			float k2 = hash12(clump, s + 23.0);
			bool pebble = k2 < 0.45;
			if (!pebble || (corner.x < 0.5 && corner.y < 0.5)) {
				drawn = true;
				if (corner.x < 0.5 && corner.y < 0.5)
					col = k2 > 0.8 ? EARTH_MID : EARTH_EDGE;
				else if (corner.x > 0.5 && corner.y > 0.5)
					col = GOLD_DEEP;
				else
					col = GOLD_SHADE;
				if (k2 > 0.62 && hash12(clump, s + 31.0) < 0.5)
					e = heat * (1.05 - 0.55 * far) * (corner.x + corner.y < 0.5 ? 1.0 : 0.8);
			}
		}
	}

	// The molten floor, crusting over from the edge in as it cools; the seams between the plates of
	// crust glow longest, and the middle longer than the wall. Plates grow with the crater, so a wide
	// one is not a web.
	if (u < 0.82) {
		float middle = 1.0 - clamp(u / 0.8, 0.0, 1.0);
		float sm = seam(p / max(7.0, rim * 0.24), s);
		float plate = heat * heat * (0.45 + 0.75 * middle) + (grain - 0.5) * 0.16;
		float line_e = heat * (0.7 + 0.55 * middle) * (1.0 - smoothstep(0.08, 0.3, sm));
		e = max(e, max(plate, line_e));
	}

	// Cracks running out from the floor, through the wall and the rim and on into the ground: one per
	// sector, at its own angle in its sector and its own length, wandering as it goes. Occlusion when
	// cold, glowing when hot - hotter toward the middle.
	// Fewer on a wide crater: at Extinction Event's size a dozen read as a spider's web.
	float n = clamp(floor(rim / 8.0), 5.0, 9.0);
	float sector = floor((a / TAU + 0.5) * n);
	float h = hash11(sector + 1.7, s);
	float ac = (sector + 0.5 + (h - 0.5) * 0.7) / n * TAU - PI;
	float off = wrap_angle(a - ac);
	float zig = sin(d * 0.27 + h * 17.0) * 1.3 + sin(d * 0.071 + h * 5.0) * 3.0 * (d / radius);
	float lateral = off * d - zig;
	float start = rim * 0.3;
	float len = min(radius - GRID * 2.0, rim * (1.1 + 0.9 * hash11(sector + 9.1, s)));
	float along = clamp((d - start) / max(GRID, len - start), 0.0, 1.0);
	float width = mix(GRID * 0.95, GRID * 0.62, along);
	if (d > start && d < len && abs(lateral) < width) {
		col = OCC;
		drawn = true;
		// The heat is in the crater: out on the ground a crack is a crack.
		e = max(e, heat * (1.12 - 0.62 * along) * (1.0 - smoothstep(1.0, 1.35, u)));
	}

	// The flash: for the first fifth of a second the bowl is white-hot, and its light runs out over the
	// ground to the radius and goes out there.
	bool front_hit = false;
	if (age < FLASH_SECONDS) {
		float ft = age / FLASH_SECONDS;
		// White at the heart, falling through the ramp to the rim - a light, not a lid.
		if (u < 1.1)
			e = max(e, (1.2 - ft * 0.4) * (1.0 - 0.55 * u * u) + (grain - 0.5) * 0.12);
		float front = mix(rim, radius - GRID, 1.0 - (1.0 - ft) * (1.0 - ft));
		front_hit = d < front && d > front - GRID * 3.0 && dith < 0.55 - 0.45 * ft;
	}

	vec3 g;
	if (front_hit) {
		col = dith < 0.3 ? FIRE_HOT : FIRE_MID;
		drawn = true;
	} else if (glow(e + (dith - 0.5) * 0.08, g)) {
		col = g;
		drawn = true;
	}

	if (!drawn)
		discard;
	COLOR = vec4(col, 1.0);
}
