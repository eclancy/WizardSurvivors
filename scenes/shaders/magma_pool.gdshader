// Pyroclasm's pool of magma (Fireball.LeaveMagmaPool, drawn by scripts/BlastPoolSurface.cs): the
// ground a fireball's blast leaves molten, burning what stands in it. On the ground, under the bodies.
//
// It was a LingeringZone drawing itself: a translucent orange polygon, a darker one inside it and a soft
// bubble - a flat wash, which at Hellfire Nova's size was a red disc over half the screen. Here it is the
// stuff that hurts, laid out to its radius (.ai/art-direction.md section 3, "a lasting zone"): a pool of
// melt with a skin of crust floating on it in plates, the melt glowing up through the seams between
// them, white-hot where it wells up in the middle and bubbling, and a bank of char round it where it has
// burnt the floor. As the pool ages the plates close up and the glow sinks into the seams, and in the
// last moments it breaks up from the edge in, by dither. crater.gdshader's floor is the model - the same
// crust, the same ramps - since Meteor Swarm's Magma Craters is the same promise as this.
//
// THE RADIUS IS THE HIT: LingeringZone.Radius, the reach its ticks test. The bank wanders, but only
// inward of it (at most 8%), so the pool never promises more ground than it burns. Every value is
// computed once per 2 px cell and every colour is an opaque Bonelight tone; Bayer dithering is the only
// gradient.
//
// PER POOL, IN THE VERTEX COLOUR (BlastPoolSurface): r how much of its life has gone (0..1), g the radius
// / 1024, b a seed, a its age in seconds / 4 (for the seep-in). The quad is its bounding square, UV 0..1.
shader_type canvas_item;
render_mode unshaded;

const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
const vec3 FIRE_CORE = vec3(1.000, 0.957, 0.816);    // fire core #FFF4D0
const vec3 FIRE_HOT = vec3(1.000, 0.784, 0.282);     // fire hot #FFC848
const vec3 FIRE_MID = vec3(0.910, 0.478, 0.102);     // fire mid #E87A1A
const vec3 FIRE_EDGE = vec3(0.722, 0.220, 0.102);    // fire edge #B8381A
const vec3 RED_LIT = vec3(0.416, 0.125, 0.157);      // red lit #6A2028
const vec3 RED_BASE = vec3(0.290, 0.094, 0.125);     // red base #4A1820
const vec3 RED_SHADE = vec3(0.165, 0.055, 0.078);    // red shade #2A0E14
const vec3 RED_DEEP = vec3(0.086, 0.027, 0.039);     // red deep #16070A
const vec3 GOLD_DEEP = vec3(0.180, 0.133, 0.031);    // gold deep #2E2208
const vec3 VIOLET_DEEP = vec3(0.078, 0.063, 0.122);  // violet deep #14101F

const float GRID = 2.0;
const float SEEP_SECONDS = 0.2;

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

vec2 hash22(vec2 p, float s) {
	vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973) + s * 0.0137);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.xx + p3.yz) * p3.zy);
}

// The distance to the nearest seam between plates of crust: Voronoi F2 - F1.
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

// Three harmonics round the bank: one frequency reads as a wobble, three as a shape.
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
	// The bank: wandering, inward of the radius only. It seeps out from a third of its size.
	float seep = mix(0.35, 1.0, clamp(age / SEEP_SECONDS, 0.0, 1.0));
	float bank = (radius - GRID) * (0.96 + 0.04 * wobble(a, s)) * seep;
	if (d > bank || radius < GRID * 4.0)
		discard;
	float u = d / bank;
	float b = bayer4(cell);
	float grain = hash12(cell, s);

	// The last eighth of its life: it breaks up from the bank inward.
	float gone = clamp((life - 0.86) / 0.14, 0.0, 1.0);
	if (b < gone * 1.3 - (1.0 - u) * 0.3)
		discard;

	// How hot it is: molten for most of its life, then the glow sinks as the crust closes over it.
	float heat = 1.0 - smoothstep(0.55, 1.0, life);
	float t = TIME;

	vec3 col;
	if (u > 0.86) {
		// The bank: floor burnt to char where the melt met it, in flakes, a few still glowing.
		float k = hash12(floor(cell / 2.0), s + 3.0);
		col = k > 0.7 ? RED_SHADE : (k > 0.25 ? RED_DEEP : OCC);
		if (k > 0.94 && heat > 0.3)
			col = FIRE_EDGE;
		// The bank's outer flakes break onto the floor rather than stopping on a line.
		if (u > 0.95 && hash12(cell, s + 7.0) < 0.45)
			discard;
	} else {
		// Plates of crust drifting slowly on the melt; bigger plates on a bigger pool.
		float plate = max(9.0, radius * 0.16);
		vec2 drift = vec2(sin(t * 0.35 + s), cos(t * 0.27 + s * 1.3)) * 0.35;
		float sm = seam(p / plate + drift, s);
		// The melt wells up in the middle: more of it glows there, and it is hotter.
		float middle = 1.0 - clamp(u / 0.75, 0.0, 1.0);
		// Thin seams toward the bank, where the crust has set; wide ones in the middle, where it is melt.
		float open = (0.05 + 0.22 * middle) * (0.45 + 0.55 * heat);
		float pulse = 0.08 * sin(t * 3.1 + sm * 9.0 + s);
		float e = heat * (1.0 - sm / max(open, 0.01)) + pulse + (b - 0.5) * 0.1;
		if (sm < open && e > 0.05) {
			// The seam: the melt showing through, white-hot where it is widest and nearest the middle.
			float lvl = e * (0.75 + 0.45 * middle);
			col = lvl > 0.82 ? FIRE_CORE : (lvl > 0.6 ? FIRE_HOT : (lvl > 0.38 ? FIRE_MID : (lvl > 0.2 ? FIRE_EDGE : RED_LIT)));
		} else {
			// The crust: dark char, a little lit where it is thin at its seams.
			float rim = 1.0 - smoothstep(open, open + 0.12, sm);
			col = rim * heat > 0.4 ? RED_BASE : (grain > 0.82 ? GOLD_DEEP : (grain > 0.3 ? RED_DEEP : RED_SHADE));
			if (grain < 0.05)
				col = VIOLET_DEEP;
		}
		// Bubbles of melt breaking the skin, white-hot, a few at a time.
		vec2 bc = floor(cell / 3.0);
		float hb = hash12(bc, s + 11.0);
		float phase = fract(t * (0.6 + 0.5 * hb) + hb * 7.0);
		if (hb > 0.95 - 0.07 * middle && phase < 0.22 * heat && u < 0.75) {
			vec2 within = cell - bc * 3.0;
			bool centre_cell = within.x > 0.5 && within.x < 1.5 && within.y > 0.5 && within.y < 1.5;
			col = centre_cell ? FIRE_CORE : FIRE_HOT;
		}
	}
	COLOR = vec4(col, 1.0);
}
