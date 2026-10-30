// Gale Blade (scripts/GaleBladeLook.cs, on scripts/ElementalBolt.cs): a crescent of moving air, spinning
// as it flies out and comes home - as pixel art on the game's 2 px grid.
//
// It was a 32x32 sprite of three arcs, spun with the bolt (sixteen radians a second), at 95% opacity, under
// a tint, and trailed by untextured particles: at every angle but the four square ones its art pixels sat
// on a rotated grid, and the particles were translucent specks smaller than an art pixel. Now, as in
// riptide_wave.gdshader, every fragment snaps itself to the WORLD's 2 px cell first and only then turns
// that cell into the blade's spinning frame to decide its colour, so the art pixels stay square and
// upright however fast it turns. Every colour is an opaque Wind tone (.ai/art-direction.md section 3):
// the cutting edge white, then hot and mid, and the inner edge in Wind's saturated edge stop - a cold
// element on a blue floor needs its edge pushed, not paled. The spin is drawn as two ghosts of the
// crescent a little behind it in its turn, a step and two down the ramp and dithered thinner each time:
// motion, not a trail of particles. Bayer dithering is the only gradient; nothing is blended.
//
// THE BLADE IS THE CUT. `blade_radius` is ElementalBolt.HitRadius plus a body (RiptideWave.BodyAllowance,
// divided back out of the node's scale): the band of enemy centres the blade cuts as it passes, the way
// the Riptide wave is drawn to its swath. Its points are on that radius, so as it spins it sweeps what it
// cuts, and an evolution's scale grows the blade and the circle together. One art pixel of Wind's edge
// hugs the cutting edge outside it: the contour that holds a white edge on a pale floor.
//
// EDDY (BoltEvolutions.EddyId): while the blade hangs at the end of its throw, `eddy_reach` is the reach
// its recut scans (ElementalBolt.EddyReach, world px) and `eddy` how far it has swollen. The air round it
// is drawn as streaks of wind wheeling about it in rings, the outermost on that reach - the stuff that
// cuts, laid out to the radius it cuts, never a ring drawn round it.
//
// The node's modulate (an evolution's tint) is overridden; a Legendary's gold is `gilded`, on the edge.
shader_type canvas_item;
render_mode unshaded;

uniform float blade_radius = 20.0;
uniform float seed = 0.0;
uniform float eddy_reach = 0.0;
uniform float eddy = 0.0;
uniform bool gilded = false;

const vec3 WIND_CORE = vec3(1.0, 1.0, 1.0);          // wind core #FFFFFF
const vec3 WIND_HOT = vec3(0.918, 0.988, 0.965);     // wind hot #EAFCF6
const vec3 WIND_MID = vec3(0.706, 0.933, 0.878);     // wind mid #B4EEE0
const vec3 WIND_EDGE = vec3(0.310, 0.682, 0.620);    // wind edge #4FAE9E
const vec3 GOLD_HI = vec3(1.0, 0.941, 0.753);        // gold hi #FFF0C0
const vec3 GOLD_LIT = vec3(0.847, 0.690, 0.290);     // gold lit #D8B04A
const float GRID = 2.0;
// The ghosts of the spin: how far behind in its turn each is drawn, in radians.
const float GHOST_STEP = 0.55;

varying vec2 v_world;
varying flat vec2 v_origin;
varying flat vec2 v_axis_x;
varying flat vec2 v_axis_y;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	v_origin = MODEL_MATRIX[3].xy;
	v_axis_x = MODEL_MATRIX[0].xy;
	v_axis_y = MODEL_MATRIX[1].xy;
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

mat2 rot(float a) {
	float c = cos(a);
	float s = sin(a);
	return mat2(vec2(c, s), vec2(-s, c));
}

// Where a point is across the crescent: < 0 outside it; otherwise 0 on the cutting edge .. 1 on the
// inner edge. The crescent is the disc of radius R less a disc 0.9 R across set 0.3 R behind its centre,
// so its belly leads and its horns sweep back to the radius.
float crescent(vec2 q, float R) {
	float d_out = R - length(q);
	float d_in = length(q + vec2(0.3 * R, 0.0)) - 0.9 * R;
	if (d_out < 0.0 || d_in < 0.0)
		return -1.0;
	return d_out / max(d_out + d_in, 0.001);
}

vec4 shade_cell(vec2 world, vec2 origin, vec2 axis_x, vec2 axis_y) {
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	mat2 m = mat2(axis_x, axis_y);
	vec2 p = inverse(m) * (centre - origin);
	float px = 1.0 / max(length(axis_x), 0.001);
	float art = GRID * px;
	float b = bayer4(cell);
	float R = blade_radius;

	// The blade. Its bands are counted in art pixels from the cutting edge, so a small blade is still
	// white, hot, mid and edge across and a big one carries more of each.
	float u = crescent(p, R);
	if (u >= 0.0) {
		float d_out = R - length(p);
		float band = floor(d_out / art);
		float span = max(1.0, floor((R * 0.4) / art));
		vec3 col;
		if (band < 1.0)
			col = gilded ? GOLD_HI : WIND_CORE;
		else if (u > 0.75 || band >= span - 1.0)
			col = gilded ? GOLD_LIT : WIND_EDGE;
		else if (u < 0.45)
			col = WIND_HOT;
		else
			col = WIND_MID;
		return vec4(col, 1.0);
	}

	// An art pixel of Wind's saturated edge round the cutting edge: a cold glow on the dark floors, and
	// the contour that keeps a white edge from vanishing on the pale ones (it did, on the Sketchbook's
	// paper, until it had one).
	{
		float d_out = R - length(p);
		float d_in = length(p + vec2(0.3 * R, 0.0)) - 0.9 * R;
		if (d_out < 0.0 && d_out >= -art && d_in >= 0.0)
			return vec4(gilded ? GOLD_LIT : WIND_EDGE, 1.0);
	}

	// Its ghosts, behind it in its turn: the shape as it was a moment ago, thinner and a step down the
	// ramp each time. The node turns clockwise, so what was there is the shape turned back.
	for (int k = 1; k <= 2; k++) {
		float fk = float(k);
		float ug = crescent(rot(GHOST_STEP * fk) * p, R);
		if (ug >= 0.0 && ug < 0.6) {
			float keep = k == 1 ? 0.55 : 0.3;
			if (b < keep) {
				return vec4(k == 1 ? WIND_MID : WIND_EDGE, 1.0);
			}
		}
	}

	// Eddy: rings of wind streaks wheeling round where the blade hangs, out to the reach it cuts. Measured
	// in world px and unturned by the blade's own spin, so they wheel at their own pace.
	if (eddy_reach > 0.0 && eddy > 0.0) {
		vec2 w = centre - origin;
		float rho = length(w);
		float inner = R / px;
		if (rho <= eddy_reach && rho > inner * 0.6) {
			vec2 n = rho > 0.01 ? w / rho : vec2(1.0, 0.0);
			float thin = 0.5 * GRID * (abs(n.x) + abs(n.y));
			float phi = atan(w.y, w.x);
			for (int j = 0; j < 4; j++) {
				float fj = float(j);
				// The outermost ring's outer pixels lie on the reach.
				float ring = mix(inner * 0.75, eddy_reach - thin, (fj + 1.0) / 4.0);
				if (abs(rho - ring) > thin)
					continue;
				// Three streaks a ring, each a third of the way round, wheeling faster nearer the blade.
				float turn = TIME * (9.0 - fj * 1.5) + hash12(vec2(fj, 1.0)) * 6.2831853;
				float a = fract((phi - turn) / 6.2831853 * 3.0);
				float len = 0.42 * eddy;
				if (a < len) {
					vec3 col = a > len - 0.06 ? WIND_HOT : (j >= 2 ? WIND_EDGE : WIND_MID);
					return vec4(col, 1.0);
				}
			}
		}
	}
	return vec4(0.0);
}

void fragment() {
	// Godot allows no return in fragment(): the cell is shaded by shade_cell, and alpha 0 is nothing.
	vec4 c = shade_cell(v_world, v_origin, v_axis_x, v_axis_y);
	if (c.a < 0.5)
		discard;
	COLOR = c;
}
