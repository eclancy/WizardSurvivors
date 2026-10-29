// Bramble Seed's thicket (scripts/BrambleSeed.cs): the thorned roots writhing out of the seed, the soil
// they tore, Runners' creepers and Bramble Keep's hedge, as pixel art on the game's 2 px grid - one quad a
// patch, under every body (DrawLayers.GroundTell).
//
// "Update all spells to the same quality we expect now" (2026-10-29). The roots were antialiased
// DrawPolylines with a disc on every joint, the soil two translucent DrawCircles, the thorns 1.5 px
// lines, the runners and the hedge antialiased lines too, and every colour a LERP - bark into grass down
// a root, everything into a dry brown at the end - at fractional alpha. Here every fragment snaps to the
// WORLD's 2 px cell first and is then measured against the roots, as thorn_vine.gdshader and
// stormsweep_lash.gdshader do; every colour is a stop of a Bonelight ramp (bark in the earth row and the
// gold row's soil, the living root in its element's ramp); Bayer dithering is the only gradient - where
// bark turns living, where it dries, and the last moment of the patch - and nothing is translucent.
//
// THE RADIUS IS THE HIT. A root is laid out in polar terms exactly as BrambleSeed lays it: its distance
// from the seed runs 0..length and length is never more than `radius` - BrambleSeed.Radius, the reach the
// tick tests - so the writhing bends a root sideways and can never carry it past the ground it hurts. The
// long roots end on the radius; that is the only edge the thicket has (no disc, no rim). A runner is drawn
// from the number its strip is tested with, and a keep's hedge stands on the rim it cuts.
//
// The shape of every root is fixed when the patch is sown and arrives once, in root_a and root_b; what
// moves is worked out here from the patch's age, as BrambleSeed's own DrawRoot did, so only scalars cross
// per frame.
shader_type canvas_item;
render_mode unshaded;

const int ROOTS = 11;

// Per root, set once: x its bearing, y its length as a share of the radius, z its delay, w its phase.
uniform vec4 root_a[11];
// Per root, set once: x its curl, y its width, z how often it whips (seconds), w unused.
uniform vec4 root_b[11];
uniform float seed = 0.0;

// Per frame.
uniform float age = 0.0;
uniform float radius = 16.0;
// 1 grown .. 0 drawn back into the ground; how dry (0..1); 1 .. 0 over the very last moment.
uniform float retract = 1.0;
uniform float dry = 0.0;
uniform float fade = 1.0;
// age / GrowSeconds, held at 1 once grown: the soil heap and the hedge rise with it.
uniform float grown = 0.0;

// Runners: xy the bearing, z where it starts (from the seed), w where its tip has crept to. w <= z: none.
uniform vec4 runner0 = vec4(0.0);
uniform vec4 runner1 = vec4(0.0);
uniform vec4 runner2 = vec4(0.0);
// Bramble Keep's hedge: x bushes round the rim (0 none), y the turn of the first, z how tall, w its rim.
uniform vec4 hedge = vec4(0.0);
// One bit a bush: set where the bush would stand in water or a wall, and is not drawn.
uniform ivec4 hedge_blocked = ivec4(0);

// The living half of the palette, repainted by an element path (Ironbriar: Metal): the root's own tone,
// its lit side, the light at its tip, and its thorns.
uniform vec4 living_edge : source_color = vec4(0.290, 0.549, 0.227, 1.0);  // grass edge #4A8C3A
uniform vec4 living_mid : source_color = vec4(0.608, 0.831, 0.333, 1.0);   // grass mid #9BD455
uniform vec4 living_hot : source_color = vec4(0.812, 0.933, 0.580, 1.0);   // grass hot #CFEE94
uniform vec4 thorn_tip : source_color = vec4(0.812, 0.933, 0.580, 1.0);    // grass hot #CFEE94

const float GRID = 2.0;
const vec2 KEY = vec2(-0.70710678, -0.70710678);
const vec3 OCC = vec3(0.020, 0.027, 0.047);           // #05070C
// Bark and soil: the earth emissive row and the gold row's lower stops (gravewell_collapse's bank).
const vec3 EARTH_MID = vec3(0.788, 0.604, 0.369);     // earth mid #C99A5E
const vec3 EARTH_EDGE = vec3(0.478, 0.314, 0.141);    // earth edge #7A5024
const vec3 GOLD_SHADE = vec3(0.329, 0.247, 0.063);    // gold shade #543F10
const vec3 GOLD_DEEP = vec3(0.180, 0.133, 0.031);     // gold deep #2E2208

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

float smooth01(float x) {
	x = clamp(x, 0.0, 1.0);
	return x * x * (3.0 - 2.0 * x);
}

float wrap_pi(float a) {
	return mod(a + PI, TAU) - PI;
}

// A living tone and its dried one, per cell: drying eats the green cell by cell.
vec3 withered(vec3 living, vec3 dried, float b) {
	return dry * 1.4 - 0.2 > b ? dried : living;
}

// Distance from p to the segment ab.
float seg_dist(vec2 p, vec2 a, vec2 b) {
	vec2 ab = b - a;
	float len2 = dot(ab, ab);
	float u = len2 > 0.0001 ? clamp(dot(p - a, ab) / len2, 0.0, 1.0) : 0.0;
	return length(p - a - ab * u);
}

// One root, at the cell p (polar d, a). True with its colour where the root, its outline, a thorn or its
// living tip covers the cell.
bool root(int i, vec2 p, float d, float a, float b, out vec3 col) {
	col = OCC;
	vec4 ra = root_a[i];
	vec4 rb = root_b[i];
	float since = age - ra.z;
	if (since <= 0.0)
		return false;
	// Erupting: out to the current radius over a quarter second, overshooting its writhe as it goes.
	float erupt = smooth01(since / 0.26);
	float len = radius * ra.y * erupt * retract;
	if (len < GRID)
		return false;
	float w = rb.y * (0.6 + 0.4 * erupt);
	float burst = 1.0 - erupt;
	// Too far round from this root's bearing for any writhe, hook or barb of it to reach: skipped before
	// any of the trigonometry, so a cell pays for the two or three roots near it, not all eleven.
	float swing = (0.38 + 0.35 * burst) * (1.0 - 0.6 * dry) + (d > len * 0.6 ? abs(rb.x) * 1.15 : 0.0);
	if (abs(wrap_pi(a - ra.x)) > swing + (w + 10.0) / max(d, 1.0))
		return false;
	// The writhe is a wave down the root; the whip a short surge of it, on each root's own clock.
	float whip_clock = mod(age + ra.w, rb.z);
	float whip = whip_clock < 0.3 ? sin(whip_clock / 0.3 * PI) : 0.0;
	float amp = (0.16 + 0.22 * whip + 0.35 * burst) * (1.0 - 0.6 * dry);
	float speed = 3.1 + 1.7 * whip;
	float breathe = 0.8 + 0.35 * sin(age * 2.3 + ra.w);
	float tip_w = w * 0.16;
	if (d > len + tip_w + GRID * 2.0)
		return false;
	float s = clamp(d / len, 0.0, 1.0);
	float k = age * speed + ra.w - s * 4.2;
	float hook_x = clamp((s - 0.62) / 0.38, 0.0, 1.0);
	float theta = ra.x + amp * s * sin(k) + rb.x * hook_x * hook_x * (3.0 - 2.0 * hook_x) * breathe;
	// The root's slope against the radial, so a hooked tip is as thick as a straight one.
	float dtheta = amp * (sin(k) - 4.2 * s * cos(k)) + rb.x * 6.0 * hook_x * (1.0 - hook_x) / 0.38 * breathe;
	float slope = s * dtheta;
	float lateral = d * wrap_pi(a - theta) / sqrt(1.0 + slope * slope);
	float side = lateral >= 0.0 ? 1.0 : -1.0;
	float lat = abs(lateral);
	// Thick at the seed, thinner in the middle, a whip at the tip: the three runs the polylines had.
	float half_w = (s < 0.4 ? w : (s < 0.7 ? w * 0.62 : w * 0.32)) * 0.5;
	half_w = max(GRID * 0.5, floor(half_w / GRID * 2.0 + 0.5) * GRID * 0.5);
	bool past_tip = d > len;
	vec3 c = OCC;
	bool hit = false;

	if (!past_tip && lat <= half_w) {
		// Bark at the seed, greening into the living root through a dithered band; the side toward the
		// key lit, the far side in shade.
		vec2 radial = vec2(cos(theta), sin(theta));
		vec2 normal = vec2(-radial.y, radial.x) * side;
		float lit = dot(normal, KEY);
		bool rim = lat > half_w - GRID;
		bool living = (s - 0.4) / 0.25 > b;
		if (!living) {
			c = EARTH_EDGE;
			if (rim)
				c = lit > 0.2 ? EARTH_MID : (lit < -0.2 ? GOLD_SHADE : c);
			c = withered(c, rim && lit < -0.2 ? GOLD_DEEP : GOLD_SHADE, b);
		} else {
			c = living_edge.rgb;
			if (rim && lit > 0.2)
				c = living_mid.rgb;
			else if (rim && lit < -0.2)
				c = EARTH_EDGE;
			c = withered(c, rim && lit > 0.2 ? EARTH_EDGE : GOLD_SHADE, b);
		}
		hit = true;
	} else if (!past_tip && lat <= half_w + GRID) {
		hit = true; // OCC: the outline that holds it on any floor
	}

	// Thorns: hooked barbs along the middle of the root, alternating sides, raked back toward the seed;
	// held inside s 0.85 and pointing sideways, so a barb never reaches past the radius.
	if (!past_tip && (!hit || lat > half_w)) {
		for (int t = 0; t < 3; t++) {
			float ts = 0.3 + float(t) * 0.2;
			float tside = mod(float(t), 2.0) < 0.5 ? 1.0 : -1.0;
			if (side != tside)
				continue;
			float barb = 3.5 + w * 0.5;
			float rise = lat - half_w;
			if (rise < 0.0 || rise > barb + GRID)
				continue;
			float centre = ts * len - rise * 0.6;
			float bw = mix(2.2, 0.5, clamp(rise / barb, 0.0, 1.0));
			float da = abs(d - centre);
			if (rise <= barb && da <= bw) {
				c = rise > barb * 0.5 ? thorn_tip.rgb : living_mid.rgb;
				c = withered(c, EARTH_MID, b);
				hit = true;
			} else if (!hit && da <= bw + GRID) {
				c = OCC;
				hit = true;
			}
		}
	}

	// The living tip, a bud of its element's light that swells as the root whips.
	if (dry < 0.6) {
		vec2 tip = vec2(cos(theta), sin(theta)) * len;
		vec2 rel = p - tip;
		float bud = GRID * (1.0 + whip);
		if (abs(rel.x) <= bud && abs(rel.y) <= bud) {
			c = abs(rel.x) <= GRID * 0.5 && abs(rel.y) <= GRID * 0.5 ? living_hot.rgb : living_mid.rgb;
			hit = true;
		}
	}
	// A clod of earth thrown up where the root first broke ground - inside the root's own length.
	if (!hit && burst > 0.05) {
		float flung = min(len, len * 0.5 + 14.0 * (1.0 - burst));
		vec2 at = vec2(cos(ra.x), sin(ra.x)) * flung;
		vec2 rel = p - at;
		if (abs(rel.x) <= GRID && abs(rel.y) <= GRID) {
			c = rel.y < 0.0 ? EARTH_MID : GOLD_SHADE;
			hit = true;
		}
	}
	col = c;
	return hit;
}

// A runner: a thorned root creeping flat along the ground out to where it has crept, swaying slowly.
bool runner(vec4 r, int index, vec2 p, float b, out vec3 col) {
	col = OCC;
	if (r.w - r.z < 4.0)
		return false;
	vec2 dir = r.xy;
	vec2 across = vec2(-dir.y, dir.x);
	float along = dot(p, dir);
	if (along < r.z - 4.0 || along > r.w + 4.0)
		return false;
	float s = clamp((along - r.z) / (r.w - r.z), 0.0, 1.0);
	float phase = root_a[index].w;
	float sway = sin(age * 2.2 + phase - s * 5.0) * 4.0 * min(1.0, s * 3.0);
	float lateral = dot(p, across) - sway;
	float lat = abs(lateral);
	bool inside = along >= r.z && along <= r.w;
	if (inside && lat <= GRID) {
		float lit = dot(across * sign(lateral), KEY);
		col = lat > GRID * 0.5 && lit > 0.2 ? living_mid.rgb : (lat > GRID * 0.5 && lit < -0.2 ? EARTH_EDGE : living_edge.rgb);
		col = withered(col, GOLD_SHADE, b);
		// Its living tip.
		if (along > r.w - GRID * 1.5 && dry < 0.6)
			col = living_hot.rgb;
		return true;
	}
	// Barbs, a pair every twenty-odd px, raked back toward the seed.
	float spacing = (r.w - r.z) / 12.0;
	if (spacing > 1.0 && inside) {
		float j = floor((along - r.z) / spacing + 0.5);
		float bside = mod(j, 2.0) < 0.5 ? 1.0 : -1.0;
		float rise = lat - GRID;
		if (sign(lateral) == bside && j >= 1.0 && j <= 11.0 && rise > 0.0 && rise <= 5.0) {
			float centre = r.z + j * spacing - rise * 0.6;
			if (abs(along - centre) <= mix(1.6, 0.5, rise / 5.0)) {
				col = withered(rise > 2.5 ? thorn_tip.rgb : living_mid.rgb, EARTH_MID, b);
				return true;
			}
		}
	}
	if (inside && lat <= GRID * 2.0)
		return true; // OCC
	return false;
}

// Bramble Keep's hedge: thorn bushes shoulder to shoulder round the rim, three canes each fanned up from
// a foot just inside the circle, barbed at their ends, rising as the keep grows.
bool hedge_at(vec2 p, float b, out vec3 col) {
	col = OCC;
	float bushes = hedge.x;
	if (bushes < 1.0 || hedge.z < 2.0)
		return false;
	float rim = hedge.w;
	float d = length(p);
	if (d < rim - 8.0 - hedge.z || d > rim + hedge.z + 4.0)
		return false;
	float a = atan(p.y, p.x);
	float step_a = TAU / bushes;
	float nearest = floor(wrap_pi(a - hedge.y) / step_a + 0.5);
	vec2 up = vec2(0.0, -1.0);
	bool hit = false;
	float best = 1e6;
	bool best_mid = false;
	vec2 best_end = vec2(0.0);
	vec2 best_foot = vec2(0.0);
	for (int o = -2; o <= 2; o++) {
		float i = mod(nearest + float(o) + bushes, bushes);
		int bit = int(i);
		int word = bit / 32;
		int mask = word == 0 ? hedge_blocked.x : (word == 1 ? hedge_blocked.y : (word == 2 ? hedge_blocked.z : hedge_blocked.w));
		if (((mask >> (bit - word * 32)) & 1) == 1)
			continue;
		float ba = hedge.y + i * step_a;
		vec2 dir = vec2(cos(ba), sin(ba));
		vec2 side = vec2(-dir.y, dir.x);
		vec2 foot = dir * (rim - 5.0);
		float sway = sin(age * 1.6 + i * 1.7) * 1.5;
		float h = hedge.z * (mod(i, 3.0) < 0.5 ? 1.0 : 0.8);
		vec2 mid = foot + up * h + side * sway;
		vec2 left = foot + (up * 0.75 - side * 0.55) * h;
		vec2 right = foot + (up * 0.75 + side * 0.55) * h;
		float dm = seg_dist(p, foot, mid);
		float dl = seg_dist(p, foot, left);
		float dr = seg_dist(p, foot, right);
		float dd = min(dm, min(dl, dr));
		if (dd < best) {
			best = dd;
			best_mid = dm <= dd;
			best_end = dm <= dd ? mid : (dl <= dr ? left : right);
			best_foot = foot;
		}
	}
	// A cane is an art pixel or two thick (the old 3.5 px stroke), in an occlusion line (its 6).
	if (best > GRID * 1.5)
		return false;
	if (best > GRID * 0.625)
		return true; // OCC: the outline
	// A cane: bark at its foot, its element's living tone up the rest with its lit edge toward the key;
	// only its last couple of px a thorn - so the bushes read one by one, not as a bright band.
	vec2 cane = best_end - best_foot;
	float u = clamp(dot(p - best_foot, cane) / max(0.0001, dot(cane, cane)), 0.0, 1.0);
	vec2 n = normalize(vec2(-cane.y, cane.x) + vec2(0.0001));
	float lit = dot(n, KEY) * sign(dot(p - best_foot, n));
	if (u > 0.9)
		col = thorn_tip.rgb;
	else if (u < 0.3)
		col = lit > 0.3 ? EARTH_MID : EARTH_EDGE;
	else
		col = lit > 0.3 ? living_mid.rgb : living_edge.rgb;
	col = withered(col, u > 0.9 ? EARTH_MID : GOLD_SHADE, b);
	return true;
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 p = (cell + 0.5) * GRID - origin;
	float b = bayer4(cell);
	// The very last of the patch goes cell by cell, never fading.
	if (fade <= b)
		discard;
	float d = length(p);
	float a = atan(p.y, p.x);
	vec3 col = OCC;
	bool hit = false;

	// The hole the seed went into, and the soil heaped round it, wider as the roots thicken.
	float mound = 5.0 + 4.0 * grown;
	if (d < mound * 0.55) {
		hit = true; // OCC
	}
	if (!hit && hedge.x > 0.0)
		hit = hedge_at(p, b, col);
	if (!hit) {
		for (int i = 0; i < ROOTS; i++) {
			vec3 c;
			if (root(i, p, d, a, b, c)) {
				col = c;
				hit = true;
				break;
			}
		}
	}
	if (!hit) {
		vec3 c;
		if (runner(runner0, 0, p, b, c) || runner(runner1, 1, p, b, c) || runner(runner2, 2, p, b, c)) {
			col = c;
			hit = true;
		}
	}
	if (!hit && d < mound + 3.0) {
		// Torn soil: clumps of the dark earth, lit on the side toward the key.
		float k = hash12(floor(cell / 2.0) + 5.0);
		col = dot(p, KEY) > 0.0 && k > 0.55 ? EARTH_MID : (k > 0.3 ? GOLD_SHADE : GOLD_DEEP);
		hit = k > 0.15 || d < mound;
	}

	if (!hit)
		discard;
	COLOR = vec4(col, 1.0);
}
