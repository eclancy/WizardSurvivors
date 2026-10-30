// Caldera (scripts/MoltenSlag.cs, Molten Shard's level 8): the pool of lava that wells up where a shard
// dives into the ground, burns what stands in it, and skins over - as pixel art on the game's 2 px grid.
//
// It was four translucent sixteen-sided polygons stacked from crust to heart, three translucent plates
// and antialiased DrawArc bubbles: soft-edged and see-through, on no grid. Now the pool is one quad and
// this shader decides each 2 px cell of it once (the game's art pixel, .ai/art-direction.md section 1),
// as crater.gdshader and gravewell_collapse.gdshader do for their ground: a bank of crust in the red
// row's dark stops, lit on its upper-left by the key; a surface of crust plates turning slowly on the
// melt, with the Fire ramp glowing up through the seams between them - white-hot at the heart, stepping
// down to mid and edge toward the bank; and bubbles of white-hot metal that swell and burst. In its last
// second it skins over: the plates spread, the seams close and step down the ramp, and then it
// dissolves from the bank inward through the Bayer matrix. Every colour is opaque; nothing is blended.
//
// THE POOL IS THE BURN. `radius` is MoltenSlag.PoolRadiusNow - the disc it burns at that moment, which
// wells up to CalderaRadius times the player's Area - and nothing is drawn past it. The bank wanders, but
// only INWARD of it, and there is no rim drawn round it: the crust bank is the lava's own edge.
//
// The quad is laid in world space round the pool's centre with the offset from that centre in its UV
// (molten_slag.gdshader's way), so this file never needs the centre itself.
shader_type canvas_item;
render_mode unshaded;

uniform float radius = 72.0;
// Seconds since the pool opened, for the plates' turning and the bubbles.
uniform float clock = 0.0;
// 0 molten .. 1 skinned over (its last second).
uniform float cooling = 0.0;
// 1 whole .. 0 gone (its last moment): it dissolves from the bank inward.
uniform float fade = 1.0;
uniform float seed = 0.0;
uniform bool gilded = false;

const vec3 FIRE_CORE = vec3(1.0, 0.957, 0.816);      // fire core #FFF4D0
const vec3 FIRE_HOT = vec3(1.0, 0.784, 0.282);       // fire hot #FFC848
const vec3 FIRE_MID = vec3(0.910, 0.478, 0.102);     // fire mid #E87A1A
const vec3 FIRE_EDGE = vec3(0.722, 0.220, 0.102);    // fire edge #B8381A
const vec3 RED_LIT = vec3(0.416, 0.125, 0.157);      // cloth red lit #6A2028
const vec3 RED_BASE = vec3(0.290, 0.094, 0.125);     // cloth red base #4A1820
const vec3 RED_SHADE = vec3(0.165, 0.055, 0.078);    // cloth red shade #2A0E14
const vec3 RED_DEEP = vec3(0.086, 0.027, 0.039);     // cloth red deep #16070A
const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
const vec3 GOLD_HI = vec3(1.0, 0.941, 0.753);        // gold hi #FFF0C0
const float GRID = 2.0;
const vec2 KEY = vec2(-0.70710678, -0.70710678);

varying vec2 v_world;
varying vec2 v_offset;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	v_offset = UV;
}

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

vec2 hash22(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973) + seed * 0.0173);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.xx + p3.yz) * p3.zy);
}

float hash12(vec2 p) {
	return hash22(p).x;
}

mat2 rot(float a) {
	float c = cos(a);
	float s = sin(a);
	return mat2(vec2(c, s), vec2(-s, c));
}

// The molten metal for a heat, 1 white-hot .. 0 dull, a step at a time, dithered at each step.
vec3 melt(float h, float b) {
	float s = clamp(h, 0.0, 1.0) * 4.0;
	float k = floor(s) + (fract(s) > b ? 1.0 : 0.0);
	if (k >= 4.0) return gilded ? GOLD_HI : FIRE_CORE;
	if (k >= 3.0) return FIRE_HOT;
	if (k >= 2.0) return FIRE_MID;
	return FIRE_EDGE;
}

vec4 shade_cell(vec2 world, vec2 offset) {
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	vec2 p = offset + (centre - world);
	float b = bayer4(cell);
	float R = radius;
	if (R < GRID)
		return vec4(0.0);
	float d = length(p);
	vec2 dir = d > 0.01 ? p / d : vec2(0.0, -1.0);
	float a = atan(p.y, p.x);

	// The bank: round, nibbled inward a pixel or two in places, never out past the burn.
	float lobe = 0.5 + 0.5 * sin(a * 5.0 + seed) * sin(a * 3.0 - seed * 1.7);
	float bank = R - GRID * floor(lobe * 2.4);
	if (d > bank)
		return vec4(0.0);
	float u = d / max(bank, 1.0);
	// Its last moment: dissolving from the bank inward.
	if (fade < 1.0 && (fade * 1.6 - u * 0.6) <= b)
		return vec4(0.0);
	float from_bank = bank - d;
	float lit = dot(dir, KEY);

	// The crust of the bank: a rim that catches the key on its upper-left, and the dark lip below it.
	if (from_bank < GRID) {
		return vec4(lit > 0.25 ? RED_LIT : (lit < -0.25 ? OCC : RED_DEEP), 1.0);
	}
	if (from_bank < GRID * 2.0) {
		return vec4(lit > 0.25 ? RED_BASE : RED_SHADE, 1.0);
	}

	// Bubbles: a few spots that swell into a ring of white-hot metal and burst, and come up elsewhere.
	for (int i = 0; i < 4; i++) {
		float fi = float(i);
		float run = clock * (0.8 + 0.23 * fi) + fi * 0.37;
		float t = fract(run);
		if (t > 0.8 || cooling > 0.6)
			continue;
		vec2 at = (hash22(vec2(fi, floor(run))) - 0.5) * 1.2 * R;
		float br = GRID * (0.6 + 2.4 * t / 0.8);
		float bd = length(p - at);
		if (bd < br + GRID * 0.5 && bd > br - GRID * 0.75) {
			return vec4(t < 0.55 ? FIRE_CORE : FIRE_HOT, 1.0);
		}
		if (bd <= br - GRID * 0.75) {
			return vec4(FIRE_MID, 1.0);
		}
	}

	// The surface: plates of crust riding on the melt and turning slowly with it - a Voronoi of plates
	// a quarter of the pool across - with the melt glowing up through the seams between them.
	float plate = max(R * 0.28, GRID * 5.0);
	vec2 s = rot(clock * 0.3) * p / plate;
	vec2 si = floor(s);
	vec2 sf = fract(s);
	float f1 = 8.0;
	float f2 = 8.0;
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 g = vec2(float(x), float(y));
			vec2 o = hash22(si + g);
			o = 0.5 + 0.38 * sin(clock * 0.6 + 6.2831853 * o);
			float dd = length(g + o - sf);
			if (dd < f1) {
				f2 = f1;
				f1 = dd;
			} else if (dd < f2) {
				f2 = dd;
			}
		}
	}
	float seam = (f2 - f1) * plate;
	// How hot the melt is here: white-hot at the heart, cooling toward the bank, and all of it stepping
	// down as the pool skins over. The heart throbs.
	float throb = 0.06 * sin(clock * 6.0);
	float heat = (1.0 - pow(u, 1.6)) * (1.0 - cooling) + throb;
	// Seams are wide in the hot heart (the plates melt) and close up toward the bank and as it cools.
	float seam_w = GRID * (0.6 + 3.2 * max(0.0, heat - 0.35)) * (1.0 - 0.7 * cooling);
	if (seam < seam_w || heat > 0.94) {
		return vec4(melt(heat + 0.04, b), 1.0);
	}
	// A plate: crust, lit on the edge that faces the key, darker where it rides low.
	vec3 col = b < 0.3 + 0.4 * cooling ? RED_SHADE : RED_BASE;
	if (seam < seam_w + GRID)
		col = heat > 0.45 ? FIRE_EDGE : RED_BASE;
	if (hash12(cell + floor(clock * 4.0)) > 0.97 && heat > 0.3)
		col = FIRE_EDGE;
	return vec4(col, 1.0);
}

void fragment() {
	// Godot allows no return in fragment(): the cell is shaded by shade_cell, and alpha 0 is nothing.
	vec4 c = shade_cell(v_world, v_offset);
	if (c.a < 0.5)
		discard;
	COLOR = c;
}
