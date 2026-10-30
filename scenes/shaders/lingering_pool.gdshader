// A LingeringZone that draws itself (scripts/LingeringZone.cs): Pyroclasm's magma pool (Fireball) and the
// Lingering Rift (Arcane Explosion) - a pool of the element lying on the ground, under the bodies.
//
// "Update all spells to the same quality we expect now" (2026-10-29). The pool was two translucent
// DrawColoredPolygons - a disc of the element's edge at 34-42% alpha with a darker one inside - and an
// antialiased DrawCircle for its bubble, fading out in alpha over its last third. Here every 2 px cell of
// the WORLD is shaded once and opaque, as mire_surface.gdshader and frost_patch.gdshader are: mostly a
// crust of occlusion, cooled thickest toward the bank, the element's own tone molten in the channels
// between its plates and their edges glowing in its hot tone, a bubble swelling and bursting - and at the
// end it dissolves from the edge in, dithered, never faded. Three tones only: occlusion, and the two the
// caller gives (FillColor and EdgeColor, the element ramp's edge and hot - their alpha is ignored).
//
// THE RADIUS IS THE HIT. `radius` is LingeringZone.Radius, the reach its tick tests. The bank wanders,
// but only INWARD of it, so the pool never promises more ground than it burns. No rim line round it.
shader_type canvas_item;
render_mode unshaded;

uniform float radius = 46.0;
uniform float age = 0.0;
uniform float life = 4.0;
uniform float seed = 0.0;
uniform vec4 body : source_color = vec4(0.722, 0.220, 0.102, 1.0);  // fire edge #B8381A
uniform vec4 glow : source_color = vec4(1.0, 0.784, 0.282, 1.0);    // fire hot #FFC848

const float GRID = 2.0;
const vec2 KEY = vec2(-0.70710678, -0.70710678);
const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
// The last share of its life over which it dissolves (the old fade began at 66%).
const float GOING = 0.34;

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

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 p = (cell + 0.5) * GRID - origin;
	float b = bayer4(cell);
	float d = length(p);
	float a = atan(p.y, p.x);

	// The bank wanders inward only, so the pool never reaches past what it burns.
	float bank = radius * (1.0 - 0.07 * (0.5 + 0.5 * sin(a * 3.0 + seed) * sin(a * 5.0 - seed * 1.3)));
	if (d > bank)
		discard;
	float inside = bank - d;
	// Going: from the edge in, cell by cell.
	float going = clamp((age / max(0.01, life) - (1.0 - GOING)) / GOING, 0.0, 1.0);
	if (going > 0.0 && b * 0.45 + (inside / max(1.0, bank)) * 0.75 < going * 1.2)
		discard;

	vec3 col;
	if (inside < GRID && dot(p, KEY) <= 0.0) {
		// The bank on the far side from the key: a dark line, its shadow. The near side has no lip of its
		// own: a lit one, in either tone, read as a stroked rim round half the pool.
		col = OCC;
	} else {
		// A crust broken into plates over the hot stuff: the plates drift a little, the seams between them
		// glow, and the pool is more crust toward its edge where it has cooled.
		float n = vnoise(p / 10.0 + vec2(age * 0.12, -age * 0.08) + seed);
		// Mostly crust - the ground it has cooled into - and more of it toward the bank; the molten stuff
		// shows in the channels between the plates, and their edges glow.
		float crust = 0.52 - 0.18 * clamp(1.0 - inside / (bank * 0.5), 0.0, 1.0);
		if (n > crust + 0.035 || (n > crust && b < 0.5))
			col = OCC;
		else if (n > crust - 0.03)
			col = glow.rgb;
		else
			col = body.rgb;
		// One bubble at a time welling up, swelling and bursting, on its own clock.
		float cycle = age * 0.85 + fract(seed * 0.37);
		float ph = fract(cycle);
		float k = floor(cycle);
		vec2 at = (vec2(hash12(vec2(k, 1.3)), hash12(vec2(k, 7.9))) * 2.0 - 1.0) * bank * 0.4;
		float rb = 2.0 + 4.0 * ph;
		float db = length(p - at);
		if (ph < 0.8 && db < rb)
			col = db > rb - GRID ? (dot(p - at, KEY) > 0.0 ? glow.rgb : body.rgb) : OCC;
		else if (ph >= 0.8 && db < 4.0)
			col = glow.rgb;
	}
	COLOR = vec4(col, 1.0);
}
