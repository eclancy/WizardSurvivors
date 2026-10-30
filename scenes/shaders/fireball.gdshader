// The fireball in flight (scripts/Fireball.cs): a ball of fire with its flame streaming back along the
// heading, as pixel art on the game's 2 px grid.
//
// It was a 32 px sprite saved at x2, shown at a scale of 1.35 to 2 and rotated to the heading - its art
// pixels 2.7 to 4 world px wide and laid on a tilted grid, beside a wizard whose pixels are 2 and
// upright - with a trail of GPU particles one world px wide in a translucent orange no ramp holds, and
// every evolution's tint multiplied over the lot, which turned Hellfire Nova's white-hot heart into a
// flat red disc. Here, as in riptide_wave.gdshader, every fragment snaps to the WORLD's 2 px cell first
// and only then turns into the ball's own frame (x along the heading, y across) to decide its colour.
// The art stays square and upright at any heading and any scale, every colour is an opaque Fire tone
// (or, for an evolution that says so, iron, light, char or gold), and Bayer dithering is the only
// gradient.
//
// SIZE. `core_radius` is the fireball's collision radius, the circle that meets an enemy and sets it
// off: the white-hot heart is drawn exactly that wide. `body` is the flame round it - the old sprite's
// size, so the ball reads as large as it did - and the flame leads the heart by little and streams
// behind it, so nothing out front promises a contact the ball has not made. Both are in node units: an
// evolution's scale grows them with the collision shape it grows.
//
// THE EVOLUTIONS ARE PAINTED, NOT TINTED. Iron Comet's heart is an iron ball lit by the key; Pyroclasm's
// flame is crusting over into plates of char with the melt glowing between them, a lava bomb; Cluster
// Bombs' three bomblets ride round inside the flame; Hellfire Nova burns down into the red rows with
// soot in its wake; Armageddon Sun is a round sun with a corona, not a comet. A Legendary's flame is gold.
shader_type canvas_item;
render_mode unshaded;

uniform float core_radius = 6.0;
uniform float body = 21.6;
uniform float time = 0.0;
uniform float seed = 0.0;
uniform bool iron = false;
uniform bool magma = false;
uniform bool cluster = false;
uniform bool hell = false;
uniform bool sun = false;
uniform bool gilded = false;

const vec3 FIRE_CORE = vec3(1.000, 0.957, 0.816);    // fire core #FFF4D0
const vec3 FIRE_HOT = vec3(1.000, 0.784, 0.282);     // fire hot #FFC848
const vec3 FIRE_MID = vec3(0.910, 0.478, 0.102);     // fire mid #E87A1A
const vec3 FIRE_EDGE = vec3(0.722, 0.220, 0.102);    // fire edge #B8381A
const vec3 LIGHT_CORE = vec3(1.0, 1.0, 1.0);         // light core #FFFFFF
const vec3 LIGHT_HOT = vec3(1.0, 0.973, 0.847);      // light hot #FFF8D8
const vec3 RED_LIT = vec3(0.416, 0.125, 0.157);      // red lit #6A2028
const vec3 RED_SHADE = vec3(0.165, 0.055, 0.078);    // red shade #2A0E14
const vec3 RED_DEEP = vec3(0.086, 0.027, 0.039);     // red deep #16070A
const vec3 VIOLET_DEEP = vec3(0.078, 0.063, 0.122);  // violet deep #14101F
const vec3 STEEL_LIT = vec3(0.722, 0.784, 0.863);    // steel lit #B8C8DC
const vec3 STEEL_BASE = vec3(0.416, 0.494, 0.596);   // steel base #6A7E98
const vec3 STEEL_SHADE = vec3(0.227, 0.290, 0.384);  // steel shade #3A4A62
const vec3 STEEL_DEEP = vec3(0.125, 0.157, 0.220);   // steel deep #202838
const vec3 GOLD_HI = vec3(1.000, 0.941, 0.753);      // gold hi #FFF0C0
const vec3 GOLD_LIT = vec3(0.847, 0.690, 0.290);     // gold lit #D8B04A
const vec3 GOLD_BASE = vec3(0.541, 0.416, 0.118);    // gold base #8A6A1E

const float GRID = 2.0;
// The key light, from the upper left in the WORLD (.ai/art-direction.md section 2).
const vec2 KEY = vec2(-0.70710678, -0.70710678);

varying vec2 world;
varying flat vec2 origin;
varying flat vec2 axis_x;
varying flat vec2 axis_y;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	origin = MODEL_MATRIX[3].xy;
	axis_x = MODEL_MATRIX[0].xy;
	axis_y = MODEL_MATRIX[1].xy;
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

vec2 hash22(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973) + seed * 0.0137);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.xx + p3.yz) * p3.zy);
}

// The distance to the nearest seam between plates: Voronoi F2 - F1 (crater.gdshader's crust).
float seam(vec2 x) {
	vec2 n = floor(x);
	vec2 f = fract(x);
	float f1 = 8.0;
	float f2 = 8.0;
	for (int j = -1; j <= 1; j++) {
		for (int i = -1; i <= 1; i++) {
			vec2 g = vec2(float(i), float(j));
			vec2 r = g + hash22(n + g) - f;
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

// Whether the world cell `cell` is the one the point `at` (node units, the ball's frame) falls in.
bool on_cell(vec2 cell, vec2 at) {
	vec2 w = origin + axis_x * at.x + axis_y * at.y;
	return all(equal(floor(w / GRID), cell));
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	mat2 m = mat2(axis_x, axis_y);
	vec2 p = inverse(m) * (centre - origin);
	// Node units per world px, so details measured in art pixels stay art pixels at any scale.
	float px = 1.0 / max(length(axis_x), 0.001);
	float b = bayer4(cell);
	float flicker = floor(time * 14.0);
	float rb = body;
	float tail = sun ? rb * 0.9 : rb * 2.6;

	// The flame: a ball that leads the heart by little, and a tongue streaming back from it that
	// narrows to nothing, rippling as it goes and torn along its edge.
	float f_head = 1.0 - length(vec2(p.x * (sun ? 1.0 : 1.3), p.y)) / rb;
	float f_tail = 0.0;
	if (p.x < 0.0 && !sun) {
		float u = clamp(-p.x / tail, 0.0, 1.0);
		float sway = sin(-p.x * 0.16 / px + time * 22.0 + seed) * rb * 0.16 * u;
		float hw = rb * pow(1.0 - u, 0.75) * (0.85 + 0.25 * sin(-p.x / rb * 2.3 - time * 17.0 + seed));
		float across = (p.y - sway) / max(hw, 0.001);
		// Tongues: streaks of flame running back along the tail, the hot ones down its middle, rippling.
		float tongue = 0.78 + 0.22 * sin(across * 5.5 + p.x / rb * 1.4 + time * 15.0 + seed);
		f_tail = (1.0 - u) * 0.85 * (1.0 - abs(across)) * tongue;
	}
	float f = max(f_head, f_tail);
	if (sun) {
		// Armageddon Sun's corona: flares licking up round the rim, on their own beat.
		float a = atan(p.y, p.x);
		float flare = pow(max(0.0, sin(a * 7.0 + time * 5.0 + seed)), 3.0) + 0.6 * pow(max(0.0, sin(a * 11.0 - time * 7.0)), 4.0);
		f = 1.0 - length(p) / (rb * (1.0 + 0.2 * flare));
	}

	vec3 col = FIRE_EDGE;
	bool hit = false;
	if (f > 0.0) {
		// Torn at the edge: the outer licks come and go in clumps of two art pixels, not grain.
		vec2 clump = floor(cell / 2.0);
		bool torn = f < 0.16 && hash12(clump + flicker * 3.7) > f / 0.16;
		if (!torn) {
			float e = f + (hash12(clump + flicker) - 0.5) * 0.08 + (b - 0.5) * 0.1;
			vec3 c0 = sun ? LIGHT_CORE : FIRE_CORE;
			vec3 c1 = sun ? LIGHT_HOT : FIRE_HOT;
			vec3 c2 = sun ? FIRE_HOT : (hell ? FIRE_EDGE : FIRE_MID);
			vec3 c3 = sun ? FIRE_MID : (hell ? RED_LIT : FIRE_EDGE);
			if (gilded) {
				c1 = GOLD_HI;
				c2 = GOLD_LIT;
				c3 = GOLD_BASE;
			}
			col = e > 0.74 ? c0 : (e > 0.52 ? c1 : (e > 0.3 ? c2 : c3));
			hit = true;

			// Pyroclasm: the flame crusting over into plates of char, the melt glowing at the seams.
			if (magma && f_head > 0.12 && f_head < 0.7) {
				float s = seam(p / (rb * 0.32) + vec2(time * 0.6, 0.0));
				if (s > 0.16)
					col = hash12(floor(p / (rb * 0.32))) > 0.5 ? RED_SHADE : RED_DEEP;
				else if (s > 0.1)
					col = RED_LIT;
			}
			// Hellfire Nova: soot boiling off the back of the flame.
			if (hell && p.x < -rb * 0.5 && f < 0.4 && hash12(clump + flicker) < 0.22)
				col = hash12(clump) > 0.5 ? VIOLET_DEEP : RED_SHADE;
		}
	}

	// The white-hot heart: exactly the circle that sets the fireball off, never less than two art pixels.
	float heart = max(core_radius, px * GRID);
	float dh = length(p);
	if (dh < heart) {
		hit = true;
		col = sun ? LIGHT_CORE : FIRE_CORE;
		if (iron) {
			// Iron Comet: an iron ball forged in the flame, lit from the key, its rim glowing.
			float lit = dot(normalize((centre - origin) + vec2(0.0001)), KEY);
			if (dh > heart - px * GRID)
				col = FIRE_HOT;
			else
				col = lit > 0.45 ? STEEL_LIT : (lit > -0.1 ? STEEL_BASE : (lit > -0.6 ? STEEL_SHADE : STEEL_DEEP));
		}
	}

	// Cluster Bombs: the three bomblets riding round inside the flame - round iron shells five art pixels
	// across, lit from the key, each with a hot heart. (Three by three they read as specks in the flame.)
	if (cluster) {
		for (int k = 0; k < 3; k++) {
			float a = time * 9.0 + float(k) * TAU / 3.0 + seed;
			vec2 at = vec2(cos(a) * 0.55, sin(a) * 0.75) * rb;
			vec2 w = origin + axis_x * at.x + axis_y * at.y;
			vec2 bc = floor(w / GRID) + 0.5;
			vec2 off = cell + 0.5 - bc;
			float r = length(off);
			if (r < 1.0) {
				col = FIRE_CORE;
				hit = true;
			} else if (r < 2.4) {
				float lit = dot(normalize(off), KEY);
				col = r < 1.6 ? FIRE_HOT : (lit > 0.4 ? STEEL_BASE : (lit > -0.3 ? STEEL_SHADE : STEEL_DEEP));
				hit = true;
			}
		}
	}

	// Sparks thrown off the back, each one art pixel, drifting back down the heading and cooling.
	for (int k = 0; k < 6; k++) {
		float fk = float(k);
		float ph = fract(time * 1.7 + fk * 0.37 + seed * 0.11);
		float gen = floor(time * 1.7 + fk * 0.37 + seed * 0.11);
		float x = -rb * 0.6 - ph * (tail + rb) * 0.9;
		float y = (hash12(vec2(fk, gen)) - 0.5) * rb * (0.6 + 1.2 * ph);
		if (on_cell(cell, vec2(x, y))) {
			col = ph < 0.25 ? FIRE_CORE : (ph < 0.5 ? FIRE_HOT : (ph < 0.75 ? FIRE_MID : FIRE_EDGE));
			if (gilded && ph >= 0.25)
				col = ph < 0.6 ? GOLD_HI : GOLD_LIT;
			hit = true;
		}
	}

	if (!hit)
		discard;
	COLOR = vec4(col, 1.0);
}
