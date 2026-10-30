// A cloud of spores or marsh gas, hanging in the air over the bodies it is in (scripts/GasCloud.cs): Contagion's
// fever clouds and plague wind, and Mirefoot's Fen Gas. One quad a cloud; every cloud a node draws shares
// this material and one batch, because each quad carries its own numbers in its vertices.
//
// "Update all spells to the same quality we expect now" (2026-10-29). Contagion's clouds were seven
// overlapping antialiased DrawCircles at 30% alpha - a soft translucent wash, doubled wherever two puffs
// overlapped - and Fen Gas a scatter of 4 px squares at random. Here every fragment snaps to the WORLD's
// 2 px cell first and every colour is a stop of the cloud's element ramp, opaque: a cloud is a ring of
// billowing lobes round a middle one, each lit on its key side and shaded under, filled by an ordered
// Bayer dither that is densest in the heart of a lobe and lets the floor and the bodies through toward its
// edge - so the swarm in it can still be read - and it thins away by that same dither, never by alpha.
//
// THE RADIUS IS THE REACH. `radius` is what the cloud infects or poisons (its caller's own test), and the
// ring's lobes are laid so their outer edges land on it and never past it: the edge of the gas is the edge
// of what it does. No disc under it and no rim round it.
//
// PER CLOUD, IN THE VERTICES:
//   UV         this corner's offset from the cloud's centre, in px - so a fragment's world position less
//              its UV is the centre, exactly (mire_surface.gdshader's waders).
//   COLOR.r    thinning: 0 whole .. 1 gone.
//   COLOR.g    the cloud's seed, 0..1.
//   COLOR.b    its radius / 256.
//   COLOR.a    its density: the most of a lobe's heart that is gas, 0..1.
shader_type canvas_item;
render_mode unshaded;

uniform vec4 ramp_core : source_color = vec4(0.973, 1.0, 0.847, 1.0);   // poison core #F8FFD8
uniform vec4 ramp_hot : source_color = vec4(0.863, 0.957, 0.549, 1.0);    // poison hot #DCF48C
uniform vec4 ramp_mid : source_color = vec4(0.706, 0.878, 0.290, 1.0);    // poison mid #B4E04A
uniform vec4 ramp_edge : source_color = vec4(0.420, 0.580, 0.125, 1.0);   // poison edge #6B9420
uniform float time = 0.0;

const float GRID = 2.0;
const int RING = 6;
const vec2 KEY = vec2(-0.70710678, -0.70710678);

varying vec2 world;
varying vec2 offset;
varying vec4 data;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	offset = UV;
	data = COLOR;
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

void fragment() {
	float thin = data.r;
	float seed = data.g * 97.0;
	float radius = data.b * 256.0;
	float density = data.a;
	vec2 centre = world - offset;
	vec2 cell = floor(world / GRID);
	vec2 rel = (cell + 0.5) * GRID - centre;
	float b = bayer4(cell);

	// The lobes: one in the middle, a ring of six round it turning slowly, each breathing on its own
	// clock. The ring's outer edges are on the radius - the reach - and never past it.
	float best_t = -1.0;
	vec2 best_n = vec2(0.0);
	float spin = time * 0.35 * (fract(seed) > 0.5 ? 1.0 : -1.0);
	for (int i = 0; i <= RING; i++) {
		float fi = float(i);
		float breathe = 1.0 + 0.07 * sin(time * 2.6 + fi * 1.9 + seed);
		vec2 c = vec2(0.0);
		float lr = radius * 0.46 * breathe;
		if (i > 0) {
			float a = seed + fi * TAU / float(RING) + spin + 0.25 * sin(seed * 3.0 + fi);
			lr = radius * (fi - 2.0 * floor(fi / 2.0) > 0.5 ? 0.40 : 0.33) * breathe;
			c = vec2(cos(a), sin(a)) * (radius - lr);
		}
		vec2 q = rel - c;
		float d = length(q);
		if (d < lr) {
			float t = 1.0 - d / lr;
			if (t > best_t) {
				best_t = t;
				best_n = d > 0.01 ? q / d : vec2(0.0, -1.0);
			}
		}
	}
	if (best_t < 0.0)
		discard;

	// Gas, not a disc: densest in the heart of a lobe, open toward its edge, and the thinning eats it
	// from the edges in. The pattern is the world's, so overlapping clouds dither as one.
	float cover = density * (0.15 + 0.65 * best_t) * (1.0 - thin * 1.15 + best_t * thin * 0.35);
	if (cover <= b)
		discard;

	// Lit on the side toward the key, shaded under it; a fleck of the white-hot core rising through
	// the heart now and then.
	float lit = dot(best_n, KEY);
	vec3 col = ramp_mid.rgb;
	if (best_t < 0.45 && lit > 0.25)
		col = ramp_hot.rgb;
	else if (best_t < 0.5 && lit < -0.2)
		col = ramp_edge.rgb;
	else if (best_t > 0.55 && hash12(cell + seed) > 0.6)
		col = ramp_hot.rgb;
	vec2 rising = floor((rel + vec2(0.0, time * 9.0)) / GRID);
	if (best_t > 0.35 && hash12(rising + seed * 7.0) > 0.975)
		col = ramp_core.rgb;
	COLOR = vec4(col, 1.0);
}
