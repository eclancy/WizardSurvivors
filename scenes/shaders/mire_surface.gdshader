// The Mirefoot trail (scripts/MireSurface.cs): every live pool of bog, drawn as ONE surface on the
// ground, under the actors.
//
// "improve the mirefoot animation. it shouldnt just draw semi transparent circles that overlap each
// other and the player." Each pool used to draw its own translucent disc at z 1, so where two pools
// overlapped the alpha doubled into a darker lens, and the newest pool sat over the wizard who had
// just stepped out of it. Here a pool is a quad that only says where to look: every 2 px cell (the
// game's art pixel, .ai/art-direction.md section 1) is shaded ONCE, by the first pool whose circle
// holds it, and that one shading reads the UNION of every pool - the deepest water any of them puts
// there. So a trail is one bog with one waterline, and nothing doubles. Every colour is opaque and a
// Bonelight tone; Bayer dithering is the only gradient, as in water_surface.gdshader.
//
// THE RADIUS IS THE HIT. `pools[i].z` is LingeringZone.Radius, the reach its damage and slow test
// against an enemy's centre. The bank wanders - organic, not a stamped circle - but only INWARD of it
// (bank_in below): nothing here is ever drawn outside a pool's radius, so the bog never promises more
// than it does. It seeps in from a third of its size, and is whole before its first bite
// (MireSurface's seep is under the zone's tick). When it dries, the water draws back toward the
// middle and leaves damp ground out to the old edge - still the full reach, still biting - and only
// the last moment of its life dissolves the whole pool.
//
// TWO KINDS OF QUAD, NO PER-FRAME ARRAYS. The pools' numbers are fixed once laid - where, how big,
// when it was born and how long it lasts - so they are uploaded only when a pool comes or goes, and
// each pool's age is worked out here from `time`. The bodies wading in it move every frame, so they are
// not uniforms at all: each is a quad of its own, drawn after the pools, with its offset from the ring
// centre in UV (shifted by WADER_UV, which is how the two kinds are told apart) and its phase in the
// vertex colour. Uploading an array every frame allocated on the C# side (a Variant of a packed array
// carries a finalizable disposer), and the swarm-heavy code allocates nothing a frame.
shader_type canvas_item;
render_mode unshaded;

// xy the pool's centre (world px), z its radius (the hit radius), w a seed.
uniform vec4 pools[32];
// x when it was laid (on `time`), y how long it lasts, z how long it takes to seep in, w how long
// its last dissolve takes. All seconds.
uniform vec4 lives[32];
uniform int pool_count = 0;
uniform float time = 0.0;

const int MAX_POOLS = 32;
const float GRID = 2.0;
// A wader's quad carries its offset in UV, shifted this far in x; a pool's carries its index.
const float WADER_UV = 1000.0;
// The share of a pool's life after which its water starts drawing in, and how far it draws in.
const float DRY_FROM = 0.55;
const float DRY_SHRINK = 0.4;
// The key light, from the upper left (.ai/art-direction.md section 2).
const vec2 KEY = vec2(-0.70710678, -0.70710678);

// The bog. Grass's verdant row is the water and the bank, the Water ramp is the sky caught on it,
// and the floor's own stone row is the ground it leaves damp. None of it is the Forest floor's moss
// teal (#24404A), or a trail laid there vanished into it, and none of it is the terrain's open water
// (arcane cyan deep), which blocks - a pool you can walk through must not read as a pond you cannot.
const vec3 OCC = vec3(0.020, 0.027, 0.047);          // occlusion #05070C - the shadowed waterline
const vec3 PEAT = vec3(0.071, 0.165, 0.039);         // verdant deep #122A0A - peat in the water
const vec3 MURK = vec3(0.086, 0.157, 0.180);         // moss deep #16282E - the dark water
const vec3 BOG = vec3(0.145, 0.329, 0.086);          // verdant shade #255416 - the shallows
const vec3 LIP = vec3(0.243, 0.541, 0.157);          // verdant base #3E8A28 - the lit bank
const vec3 SCUM = vec3(0.290, 0.549, 0.227);         // grass edge #4A8C3A - duckweed
const vec3 WEED = vec3(0.608, 0.831, 0.333);         // grass mid #9BD455 - a lit frond of it
const vec3 SKY_EDGE = vec3(0.165, 0.373, 0.769);     // water edge #2A5FC4 - a ripple in shadow
const vec3 SKY_MID = vec3(0.435, 0.722, 0.941);      // water mid #6FB8F0 - a ripple in the light
const vec3 SKY_HOT = vec3(0.706, 0.863, 1.0);        // water hot #B4DCFF - a bubble bursting
const vec3 DAMP = vec3(0.078, 0.102, 0.149);         // floor stone shade #141A26 - wet ground
const vec3 DAMP_DEEP = vec3(0.043, 0.059, 0.094);    // floor stone deep #0B0F18 - wetter ground

varying vec2 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
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

// How far in from its radius a pool's bank sits here, as a share 0..1 of the most it may (bank_in).
// In WORLD space, so where two pools meet their banks are one bank, and fixed once laid: a puddle
// whose edge crawled every frame read as a simulation (LingeringZone's old note). It depends on the
// cell alone, so it is worked out once a fragment, not once a pool.
float bank_noise(vec2 p) {
	return 0.65 * vnoise(p / 26.0) + 0.35 * vnoise(p / 10.0 + vec2(17.3, 4.1));
}

// The most a pool's bank may wander in from its radius, in px.
float bank_in(float radius) {
	return clamp(radius * 0.08, 3.0, 10.0);
}

// Where pool i is in its life: x seeped in 0..1, y dried 0..1, z dissolved 0..1.
vec3 life_of(int i) {
	vec4 l = lives[i];
	float age = time - l.x;
	float gone_at = l.y - l.w;
	float dry_at = min(gone_at, l.y * DRY_FROM);
	return vec3(
		clamp(age / max(0.01, l.z), 0.0, 1.0),
		smoothstep(dry_at, max(dry_at + 0.01, gone_at), age),
		clamp((age - gone_at) / max(0.01, l.w), 0.0, 1.0));
}

// The union of every pool that reaches p: how far inside the wet ground (wet) and the water (water)
// it is, whichever pool puts it deepest; how present the bog still is (presence, falling to 0 as the
// last pool here dissolves); how fresh the water is (1 while still seeping in); and the pool that
// shapes the shore here (lead, lead_r).
void bog_at(vec2 p, int fallback, out float wet, out float water, out float presence, out float fresh, out vec2 lead, out float lead_r) {
	wet = -1e4;
	water = -1e4;
	presence = 0.0;
	fresh = 0.0;
	lead = pools[fallback].xy;
	lead_r = pools[fallback].z;
	float lead_v = -1e4;
	float bank = bank_noise(p);
	for (int i = 0; i < MAX_POOLS; i++) {
		if (i >= pool_count) break;
		vec4 pool = pools[i];
		float dist = length(p - pool.xy);
		if (dist > pool.z) continue;
		vec3 l = life_of(i);
		// Seeps in from a third of its size, fast and then slowing.
		float spread = mix(0.33, 1.0, 1.0 - (1.0 - l.x) * (1.0 - l.x));
		float edge = (pool.z - bank_in(pool.z) * bank) * spread;
		float w = edge - dist;
		// Drying: the water draws in toward the middle; the ground it leaves stays dark and wet.
		float shore = edge * (1.0 - DRY_SHRINK * l.y) - dist;
		if (w > wet) wet = w;
		if (shore > water) water = shore;
		if (w > 0.0) {
			presence = max(presence, 1.0 - l.z);
			fresh = max(fresh, 1.0 - l.x);
		}
		if (shore > lead_v) { lead_v = shore; lead = pool.xy; lead_r = pool.z; }
	}
}

// Rings rolling out round a wader's feet. rel is the cell from the ring centre; phase its own rhythm.
bool wader_ring(vec2 rel, float phase, float b, out vec3 col) {
	col = vec3(0.0);
	float ring = length(rel * vec2(1.0, 2.0));
	bool hit = false;
	for (int k = 0; k < 2; k++) {
		float t = fract(time * 0.9 + phase + float(k) * 0.5);
		float rr = 12.0 + 28.0 * t;
		if (abs(ring - rr) < 1.5 && (1.0 - t) * 1.2 > b) {
			col = t < 0.6 ? SKY_MID : SKY_EDGE;
			hit = true;
		}
	}
	return hit;
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 p = cell * GRID + GRID * 0.5;
	float b = bayer4(cell);
	float wet, water, presence, fresh, lead_r;
	vec2 lead;

	if (UV.x > WADER_UV * 0.5) {
		// A wader's quad: only its rings, and only on open water. Its centre is this fragment's
		// world position less its offset from the centre (both interpolated, so exact).
		vec2 ring_centre = world - vec2(UV.x - WADER_UV, UV.y);
		bog_at(p, 0, wet, water, presence, fresh, lead, lead_r);
		vec3 ring_col;
		if (water <= 2.0 || presence <= b || !wader_ring(p - ring_centre, COLOR.r, b, ring_col))
			discard;
		COLOR = vec4(ring_col, 1.0);
	} else {
		// A pool's quad. Shaded once: by the first pool whose circle holds this cell. Every other
		// pool's quad that covers it lets it go, so overlaps cost a test, not a second layer of water.
		int me = int(UV.x + 0.5);
		int owner = -1;
		for (int i = 0; i < MAX_POOLS; i++) {
			if (i >= pool_count) break;
			vec2 d = p - pools[i].xy;
			if (dot(d, d) <= pools[i].z * pools[i].z) { owner = i; break; }
		}
		if (owner != me) discard;

		bog_at(p, owner, wet, water, presence, fresh, lead, lead_r);
		if (wet <= 0.0) discard;
		// A ragged bank, dithered into the ground over its last 3 px, and the last of a pool's life
		// dissolving it rather than fading it - nothing here is ever translucent.
		if (wet < 3.0 && wet / 3.0 <= b) discard;
		if (presence <= b) discard;

		// Which way the bank faces. The key is upper left: where the bank is up-left of the water it
		// throws a shadow onto it, where it is down-right its lip catches the light.
		vec2 out_dir = p - lead;
		out_dir = dot(out_dir, out_dir) > 0.01 ? normalize(out_dir) : vec2(0.0, 1.0);
		float facing = -dot(out_dir, KEY);

		// The depth is measured from the union's shore and saturates a few pixels in, so the middle of
		// the trail is one body of water rather than a dark bowl at every pool's centre.
		vec3 col;
		if (water <= 0.0) {
			// Ground the water has drawn back from: a mottled stain, wettest at the waterline and
			// thinning out to the old edge - darkened ground, not a ring drawn round the pool.
			float soak = clamp(1.0 + water / max(4.0, wet - water), 0.0, 1.0);
			float mottle = vnoise(p / 5.0);
			if (0.2 + 0.6 * soak + 0.35 * (mottle - 0.5) <= b) discard;
			col = (water > -3.0 && b < 0.5) ? MURK : (mottle > 0.55 ? DAMP_DEEP : DAMP);
		} else if (fresh > 0.0 && wet < 2.0 + 6.0 * fresh) {
			// The front of water still welling up through the ground: soaked earth going under.
			col = hash12(cell + floor(time * 12.0)) > 0.5 ? DAMP_DEEP : BOG;
		} else if (water < 2.0) {
			// The waterline: lit lip on the sunward bank, a dark line under the shadowed one.
			col = facing > 0.3 ? LIP : (facing < -0.3 ? OCC : BOG);
		} else {
			// The body: dark water, with peat stirred through it in slow clouds.
			float silt = vnoise(p / 22.0 + vec2(time * 0.06, -time * 0.04));
			col = (silt - 0.42) * 6.0 > b ? PEAT : MURK;
			// The shallows: green over the bank on the sunward side, peat and shadow under the far one.
			float shallows = 2.0 + 4.0 * clamp(facing + 0.4, 0.0, 1.0);
			float shadow = 2.0 + 6.0 * clamp(-facing, 0.0, 1.0);
			if (facing < 0.0 && water < shadow) {
				col = (water - 2.0) / max(1.0, shadow - 2.0) > b ? col : PEAT;
			} else if (water < 2.0 + shallows) {
				col = (water - 2.0) / shallows > b ? col : BOG;
			}
			// Duckweed in the shallows, drifting a little.
			float weed = vnoise(p / 6.0 + vec2(time * 0.10, time * 0.04));
			if (water < 10.0 && weed > 0.66 && hash12(cell) > 0.45) col = weed > 0.8 ? WEED : SCUM;
			// Ripples: short horizontal lines of sky on every third row, drifting slowly one way or
			// the other, more of them and brighter where a sheen slides across the whole bog.
			float sweep = fract(dot(p, vec2(0.003, 0.002)) - time * 0.04);
			float sheen = smoothstep(0.0, 0.06, sweep) * (1.0 - smoothstep(0.06, 0.22, sweep));
			if (water > 4.0 && mod(cell.y, 3.0) < 1.0) {
				float drift = (hash12(vec2(cell.y, 1.7)) > 0.5 ? 1.0 : -1.0) * (1.5 + 2.0 * hash12(vec2(cell.y, 5.3)));
				float x = (cell.x + drift * time) / 8.0;
				if (fract(x) < 0.45 && hash12(vec2(floor(x), cell.y)) < 0.14 + 0.5 * sheen)
					col = sheen > 0.5 ? SKY_MID : SKY_EDGE;
			}
			// Bubbles working up through it, two to a pool: swell, burst, and a ring rolls out.
			for (int i = 0; i < MAX_POOLS; i++) {
				if (i >= pool_count) break;
				vec4 pool = pools[i];
				if (length(p - pool.xy) > pool.z) continue;
				for (int k = 0; k < 2; k++) {
					vec2 hk = vec2(pool.w * 7.13 + float(k) * 3.7, pool.w * 1.91 + float(k) * 11.3);
					vec2 at = pool.xy + (vec2(hash12(hk), hash12(hk + 5.1)) * 2.0 - 1.0) * pool.z * 0.42;
					float period = 1.7 + 1.3 * hash12(hk + 9.7);
					float ph = fract(time / period + hash12(hk + 2.3));
					vec2 rel = p - at;
					if (ph < 0.5) {
						float rad = 1.0 + 3.0 * (ph / 0.5);
						float d = length(rel);
						if (d < rad) col = (d > rad - 2.0) ? SKY_EDGE : PEAT;
						// Its lit side, toward the key.
						if (d < rad && d > rad - 2.5 && dot(rel, KEY) > 0.0) col = SKY_MID;
					} else if (ph < 0.56) {
						if (length(rel) < 3.0) col = SKY_HOT;
					} else {
						float t = (ph - 0.56) / 0.44;
						float ring = length(rel * vec2(1.0, 1.7));
						float rr = 4.0 + 14.0 * t;
						if (abs(ring - rr) < 1.2 && (1.0 - t) > b) col = t < 0.3 ? SKY_MID : SKY_EDGE;
					}
				}
			}
		}
		COLOR = vec4(col, 1.0);
	}
}
