// Slag Trail (scripts/MoltenSlag.cs, Molten Shard's level 4): the run of molten droplets a shard's flight
// spills on the ground, cooling and going out - as pixel art on the game's 2 px grid.
//
// It was three passes of translucent DrawCircle and DrawLine per point - a smooth, antialiased stroke
// whose overlaps doubled their alpha - and a ring of fading translucent squares for embers. Now every
// droplet is one quad, and this shader decides each 2 px cell of it once (the game's art pixel,
// .ai/art-direction.md section 1): a lump of crust in the red row's dark stops with the molten metal in
// it in the Fire ramp, white-hot while it is fresh and stepping down the ramp - hot, mid, edge - as it
// cools, the crust closing over it, until the last of it dissolves through the Bayer matrix. Its lit
// side, toward the key from the upper left, catches a step lighter. One ember rises off each hot lump.
// Every colour is opaque; nothing is blended.
//
// ONE QUAD A DROPLET, NO PER-FRAME ARRAYS (MoltenSlag._Draw, mire_surface.gdshader's way): the quad is
// laid in world space round the droplet's centre, with the offset from that centre in its UV, and the
// droplet's numbers in its vertex colour:
//   r  heat: 1 just laid, 0 gone out. MoltenSlag works it out from when the point was laid.
//   g  a fixed 0..1 for the droplet, so it keeps its shape and its ember from frame to frame.
//   b  its radius, / 16. NOTHING IS DRAWN PAST IT, and MoltenSlag keeps it inside SlagHalfWidth - the
//      half-width its burn tests against - so the slag is never drawn wider than it burns.
//   a  unused (1).
shader_type canvas_item;
render_mode unshaded;

const vec3 FIRE_CORE = vec3(1.0, 0.957, 0.816);      // fire core #FFF4D0
const vec3 FIRE_HOT = vec3(1.0, 0.784, 0.282);       // fire hot #FFC848
const vec3 FIRE_MID = vec3(0.910, 0.478, 0.102);     // fire mid #E87A1A
const vec3 FIRE_EDGE = vec3(0.722, 0.220, 0.102);    // fire edge #B8381A
const vec3 RED_BASE = vec3(0.290, 0.094, 0.125);     // cloth red base #4A1820
const vec3 RED_SHADE = vec3(0.165, 0.055, 0.078);    // cloth red shade #2A0E14
const vec3 RED_DEEP = vec3(0.086, 0.027, 0.039);     // cloth red deep #16070A
const float GRID = 2.0;
const vec2 KEY = vec2(-0.70710678, -0.70710678);

varying vec2 v_world;
varying vec2 v_offset;
varying flat vec4 v_data;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	v_offset = UV;
	v_data = COLOR;
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

// The molten metal's colour for a heat, 1 white-hot .. 0 dull, a step at a time, dithered at each step.
vec3 melt(float h, float b) {
	float s = clamp(h, 0.0, 1.0) * 4.0;
	float step = floor(s);
	bool up = fract(s) > b;
	float k = step + (up ? 1.0 : 0.0);
	if (k >= 4.0) return FIRE_CORE;
	if (k >= 3.0) return FIRE_HOT;
	if (k >= 2.0) return FIRE_MID;
	return FIRE_EDGE;
}

vec4 shade_cell(vec2 world, vec2 offset, vec4 data) {
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	// The offset of this cell's centre from the droplet's: the quad is world-aligned, so the UV moves one
	// for one with the world.
	vec2 p = offset + (centre - world);
	float heat = data.r;
	float id = data.g;
	float r = data.b * 16.0;
	float b = bayer4(cell);
	if (heat <= 0.0 || r < 1.0)
		return vec4(0.0);

	// The ember: one art pixel of white-hot metal flicked up off some of the hot lumps, rising and going
	// out.
	if (heat > 0.4 && id < 0.45) {
		float rise = (1.0 - heat) / 0.6;
		vec2 ember = vec2((id - 0.5) * r * 1.2, -r - GRID - rise * GRID * 4.0);
		// The droplet's centre in the world is this fragment less its offset from it.
		if (floor((world - offset + ember) / GRID) == cell && fract(TIME * 5.0 + id * 7.0) < 0.75) {
			return vec4(rise < 0.5 ? FIRE_CORE : FIRE_HOT, 1.0);
		}
	}

	// The lump: round, its edge knocked in an art pixel here and there - inward only.
	float d = length(p);
	float a = atan(p.y, p.x);
	float sector = floor((a + 3.14159265) / 0.9);
	float edge = r - (hash12(vec2(sector, id * 97.0)) > 0.6 ? GRID : 0.0);
	if (d > edge)
		return vec4(0.0);
	// The last of it dissolves.
	if (heat < 0.15 && heat / 0.15 <= b)
		return vec4(0.0);

	float from_edge = edge - d;
	vec2 dir = d > 0.01 ? p / d : vec2(0.0, -1.0);
	float lit = dot(dir, KEY);
	// The molten heart shrinks as the crust closes over it; it is gone before the slag goes out.
	float molten = r * (0.15 + 0.7 * heat);
	vec3 col;
	if (d < molten) {
		// Hottest in the middle, a step cooler toward the crust, and it seethes.
		float seethe = hash12(cell + floor(TIME * 10.0) + id) < 0.2 ? -0.18 : 0.0;
		col = melt(heat * (1.15 - 0.35 * d / max(molten, 0.01)) + seethe, b);
	} else if (from_edge < GRID) {
		// The crust's rim: catching the key on its upper-left, in shadow on its lower-right.
		col = lit > 0.3 ? RED_BASE : RED_DEEP;
	} else {
		// Crust, with cracks of the glow still showing through it while it is warm.
		bool crack = hash12(cell + id * 13.0) > 0.86 - heat * 0.3;
		col = crack && heat > 0.25 ? FIRE_EDGE : (b < 0.35 ? RED_BASE : RED_SHADE);
	}
	return vec4(col, 1.0);
}

void fragment() {
	// Godot allows no return in fragment(): the cell is shaded by shade_cell, and alpha 0 is nothing.
	vec4 c = shade_cell(v_world, v_offset, v_data);
	if (c.a < 0.5)
		discard;
	COLOR = c;
}
