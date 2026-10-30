// The Buried Runes' drift field (scripts/BuriedRunes.cs; .ai/achievements-and-secrets.md 8.8): a place on
// the Frozen Waste where the snow never stops blowing, except round anyone who stands still. Two quads of
// this shader, every fragment snapped to the WORLD's 2 px cell and shaded once, opaque, from the Bonelight
// rows - bone's upper stops for the snow, Ice's ramp at a streak's head - with Bayer dithering the only
// gradient (.ai/art-direction.md sections 1 and 3).
//
// `layer` 1, in the air over the bodies: the blowing snow. Streaks a cell thick running with the wind in
// lanes four cells apart, each lane at its own speed, a white head and a tail that dithers out; dense in
// the field, thinning out over two chunks round it (the far tell: "the blowing snow, visible from two
// chunks"), and never more than a scatter of cells - the horde in it must still read.
//
// `layer` 0, on the ground under the bodies: the calm. Where he has stood still, the wind drops round him
// in a circle that grows to `calm_radius` (BuriedRunes.RevealRadius when it has settled): no streaks
// above it, and on the ground the snow lying still, thickest at its rim. When he moves on, the calm
// dissolves (`calm_strength`) and the snow blows over it again.
shader_type canvas_item;
render_mode unshaded;

uniform int layer = 1;
uniform vec2 centre = vec2(0.0);
uniform float field_radius = 640.0;
uniform float outer_radius = 3072.0;
uniform float time = 0.0;
// The way the wind blows (where the snow goes), unit length.
uniform vec2 wind = vec2(-0.97, 0.24);
uniform vec2 calm_centre = vec2(0.0);
uniform float calm_radius = 0.0;
uniform float calm_strength = 0.0;

const float GRID = 2.0;

const vec3 SNOW_HI = vec3(0.933, 0.949, 0.980);    // bone hi #EEF2FA
const vec3 SNOW_LIT = vec3(0.831, 0.863, 0.918);   // bone lit #D4DCEA
const vec3 SNOW_BASE = vec3(0.604, 0.643, 0.722);  // bone base #9AA4B8
const vec3 SNOW_SHADE = vec3(0.384, 0.431, 0.518); // bone shade #626E84
const vec3 ICE_HOT = vec3(0.784, 0.957, 1.0);      // ice hot #C8F4FF

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

// How still the air is here: 1 inside the calm, falling to 0 over a ragged edge.
float calm_at(vec2 p) {
	if (calm_strength <= 0.0 || calm_radius <= 0.0)
		return 0.0;
	float edge = calm_radius * (0.92 + 0.08 * vnoise(p / 30.0));
	float d = length(p - calm_centre);
	return calm_strength * (1.0 - smoothstep(edge - 20.0, edge, d));
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 p = cell * GRID + GRID * 0.5;
	float b = bayer4(cell);
	float d = length(p - centre);
	if (d > outer_radius)
		discard;
	float calm = calm_at(p);
	vec3 col = vec3(0.0);
	bool on = false;

	if (layer == 1) {
		// The blowing snow, everywhere but the calm.
		if (calm > b)
			discard;
		float density = d < field_radius ? 1.0 : pow(1.0 - (d - field_radius) / (outer_radius - field_radius), 2.0);
		vec2 across_dir = vec2(-wind.y, wind.x);
		float across = dot(p, across_dir);
		float along = dot(p, wind);
		float lane = floor(across / 8.0);
		// One cell thick, lanes four cells apart.
		if (mod(across, 8.0) < 2.0) {
			float speed = 170.0 + 130.0 * hash12(vec2(lane, 3.1));
			float u = along - time * speed + hash12(vec2(lane, 7.7)) * 997.0;
			float seg = floor(u / 64.0);
			float f = fract(u / 64.0);
			float share = 0.10 + 0.22 * hash12(vec2(seg, lane));
			if (hash12(vec2(seg * 1.3, lane + 0.5)) < density * 0.4 && f < share) {
				// Its head is the end it is blowing toward: the far end of the segment.
				float t = f / share;
				if (t > 0.86)
					col = SNOW_HI;
				else if (t > 0.5)
					col = SNOW_LIT;
				else if (t * 2.0 > b)
					col = SNOW_BASE;
				else
					discard;
				on = true;
			}
		}
		// The odd flake tumbling between the lanes, mostly near the middle.
		if (!on) {
			vec2 drift = p - wind * time * 120.0;
			vec2 fc = floor(drift / 14.0);
			if (hash12(fc) < 0.05 * density && length(fract(drift / 14.0) - vec2(0.5)) < 0.08) {
				col = hash12(fc + 3.0) > 0.7 ? ICE_HOT : SNOW_LIT;
				on = true;
			}
		}
	} else {
		// The calm: clear ground where the wind has dropped - which is what shows the stones - with the snow
		// it dropped heaped in a ragged drift round its rim. Solid cells with noise edges, never a dithered
		// wash; only its fading (calm_strength) dissolves it.
		if (calm_strength <= b || calm_radius <= 0.0)
			discard;
		float r = length(p - calm_centre);
		float edge = calm_radius * (0.92 + 0.08 * vnoise(p / 30.0));
		float inside = edge - r;
		if (inside < 0.0)
			discard;
		float band = 3.0 + 7.0 * vnoise(p / 22.0 + vec2(4.0, 1.0));
		if (inside < band && vnoise(p / 40.0 + vec2(9.0, 2.0)) > 0.28) {
			// The drift: its crest toward the outside, lit; its inner slope down to the clear ground.
			float t = inside / band;
			col = t < 0.3 ? SNOW_HI : (t < 0.7 ? SNOW_LIT : SNOW_BASE);
			on = true;
		}
		// Inside the drift, nothing: clear, still ground is what shows the stones (patches of lying snow
		// were tried, and at any size they read as speckle or as blobs over the very stones it uncovers).
	}
	if (!on)
		discard;
	COLOR = vec4(col, 1.0);
}
