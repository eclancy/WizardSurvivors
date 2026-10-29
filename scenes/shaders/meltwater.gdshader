// Frost Shard's Meltwater (scripts/FrostShatter.cs): the pool a corpse melts into, waiting for the next
// shatter to freeze it. One quad a pool; as in frost_patch.gdshader every 2-unit cell of the WORLD (the
// game's art pixel) is shaded once, in opaque Water and Ice tones, with Bayer dithering the only gradient.
//
// It was two translucent squashed DrawCircles and an antialiased DrawArc - a wash, not water. Now it is
// water lying on the floor: the deep water edge dithered thin enough that the floor shows through it,
// rings of the water's mid running out from where the body fell, a sheen of the ice's hot light on the
// side that faces the key (.ai/art-direction.md section 2), and a rim that wanders only INWARD of the
// pool's reach. It spreads out of the corpse in its first moments and, in its last second, dries from
// the edge in, dithered rather than faded.
//
// THE RADIUS IS THE FREEZE. `radius` is the pool's reach (FrostShatter.Pool.Radius), the distance its
// freeze measures - a circle, as the freeze is, where it used to be drawn squashed. No ring marks it.
shader_type canvas_item;
render_mode unshaded;

uniform float radius = 50.0;
// 0..1: how far it has spread out of the corpse, and how much of it is left as it dries.
uniform float spread = 1.0;
uniform float wet = 1.0;
uniform float time = 0.0;
uniform float seed = 0.0;

const float GRID = 2.0;
const vec3 WATER_CORE = vec3(0.941, 0.980, 1.0);     // water core #F0FAFF
const vec3 WATER_MID = vec3(0.435, 0.722, 0.941);    // water mid #6FB8F0
const vec3 WATER_EDGE = vec3(0.165, 0.373, 0.769);   // water edge #2A5FC4
const vec3 ICE_HOT = vec3(0.784, 0.957, 1.0);        // ice hot #C8F4FF
const vec2 KEY = vec2(-0.70710678, -0.70710678);

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

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 q = cell * GRID + GRID * 0.5 - origin;
	float b = bayer4(cell);
	float d = length(q);
	float a = atan(q.y, q.x);
	// The rim wanders inward only, so the water never promises more ground than its freeze reaches.
	float wander = 0.5 + 0.5 * sin(a * 5.0 + seed) * sin(a * 3.0 - seed * 0.7);
	float rim = radius * spread * (0.9 + 0.1 * wander);
	if (d > rim || rim < GRID)
		discard;
	float t = d / rim;
	// Drying from the edge in.
	if (t > wet * 1.15 - 0.15 + 0.15 * b)
		discard;
	vec2 dir = d > 0.01 ? q / d : vec2(0.0, -1.0);
	float lit = dot(dir, KEY);
	vec3 col = WATER_EDGE;
	bool keep;
	if (rim - d < GRID * 1.5 && lit > 0.45) {
		// The near lip catches the ice's light where it faces the key - only there, so no line runs
		// round the pool.
		col = ICE_HOT;
		keep = true;
	} else {
		// Ripples running out from the middle, broken into short arcs so they read as moving water and
		// never as rings round a target, and the sheen on the key's side.
		float wave = d / 16.0 - time * 0.8;
		float arc = floor(a * 7.0 / 6.2831853 + floor(wave) * 3.0 + seed);
		bool ripple = fract(wave) < 0.1 && fract(sin(arc * 12.9898) * 43758.5453) > 0.45;
		if (ripple && t > 0.15) {
			col = WATER_MID;
			keep = true;
		} else if (lit > 0.55 && t > 0.55 && t < 0.8) {
			col = ICE_HOT;
			keep = 0.7 > b;
		} else if (t < 0.12) {
			col = WATER_CORE;
			keep = 0.25 > b;
		} else {
			col = WATER_EDGE;
			keep = 0.5 > b;
		}
	}
	if (!keep)
		discard;
	COLOR = vec4(col, 1.0);
}
