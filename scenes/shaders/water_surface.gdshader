// Open water, drawn from a distance field (scripts/WaterSurface.cs) and coloured from generated art
// (tools/art/water.py). See .ai/terrain.md section 3.
//
// TEXTURE is the chunk's signed distance to the shore: one byte a 6 px texel, +-12 px, positive in
// the water. It is read with bilinear filtering at the MIDDLE OF EACH WORLD PIXEL, so the shore is
// a smooth curve that still lands on the art's pixel grid - the same grid every tile is drawn on.
// Every colour is picked from the tones strip or the surface tile; nothing is blended, so nothing
// off the Bonelight contract can appear, and dithering is the only gradient (as everywhere else).
shader_type canvas_item;

uniform sampler2D surface_tex : filter_nearest, repeat_enable;
uniform sampler2D ripple_tex : filter_nearest, repeat_enable;
uniform sampler2D tones : filter_nearest;
// Width of the mud lip on the bank and of the dark band under it, in px.
uniform float lip_width = 6.0;
uniform float edge_width = 7.0;
// Ripple crests: px per second, two layers crossing. Slow - the water should move, not flow.
uniform vec2 ripple_drift_a = vec2(2.0, 0.5);
uniform vec2 ripple_drift_b = vec2(-1.3, 0.9);

// The tones strip (water.py TONE_SLOTS). The order is an API.
const int TONE_LIP_OUTER = 0;
const int TONE_LIP_INNER = 1;
const int TONE_WATERLINE = 2;
const int TONE_EDGE = 3;
const int TONE_RIPPLE = 4;
const int TONE_SHALLOWS = 7;

varying vec2 world;
varying flat vec2 origin;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	origin = MODEL_MATRIX[3].xy;
}

float bayer4(vec2 p) {
	int x = int(mod(p.x, 4.0));
	int y = int(mod(p.y, 4.0));
	int i = x + y * 4;
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[i] + 0.5) / 16.0;
}

vec4 tone(int i) {
	return texelFetch(tones, ivec2(i, 0), 0);
}

bool ripple_at(vec2 px, vec2 drift, vec2 offset) {
	vec2 size = vec2(textureSize(ripple_tex, 0));
	vec2 p = floor(px + offset + drift * TIME);
	return texture(ripple_tex, (p + 0.5) / size).a > 0.5;
}

// The field is one texel per TEXEL_PX world pixels (WaterSurface.TexelPx).
const float TEXEL_PX = 6.0;
// And the distance is stored as one byte over +-RANGE px (WaterSurface.Range).
const float RANGE = 12.0;

float field(sampler2D tex, vec2 local, vec2 texel) {
	return texture(tex, local * texel / TEXEL_PX).r * 2.0 * RANGE - RANGE;
}

void fragment() {
	vec2 cellpx = floor(world);
	vec2 local = cellpx + 0.5 - origin;
	float d = field(TEXTURE, local, TEXTURE_PIXEL_SIZE);
	if (d < -lip_width) {
		discard;
	}
	float b = bayer4(cellpx);

	// Which way the shore faces. The key light is from the upper left (art-direction.md section 2),
	// so where the bank is up-left of the water it throws a shadow onto it - a wider dark band - and
	// where the bank is down-right its lip is in the light. The field's gradient points into the
	// water; facing is +1 for a shore lit side on, -1 for one in its bank's shadow.
	vec2 grad = vec2(field(TEXTURE, local + vec2(1.0, 0.0), TEXTURE_PIXEL_SIZE) - field(TEXTURE, local - vec2(1.0, 0.0), TEXTURE_PIXEL_SIZE),
		field(TEXTURE, local + vec2(0.0, 1.0), TEXTURE_PIXEL_SIZE) - field(TEXTURE, local - vec2(0.0, 1.0), TEXTURE_PIXEL_SIZE));
	float facing = length(grad) > 0.01 ? -dot(normalize(grad), vec2(0.7071, 0.7071)) : 0.0;

	if (d < 0.0) {
		// The bank: a ring of damp earth dithered out into the ground, wettest at the water. Its
		// colour is a step from the ground's, never a line drawn round the pond.
		float out_d = -d;
		float cover = (1.0 - out_d / lip_width) * (0.85 + 0.15 * facing);
		if (cover <= b) {
			discard;
		}
		COLOR = (out_d < 1.5 && b < 0.55) ? tone(TONE_LIP_INNER) : tone(TONE_LIP_OUTER);
	} else {
		vec2 size = vec2(textureSize(surface_tex, 0));
		vec4 col = texture(surface_tex, (cellpx + 0.5) / size);
		// Dark water under the bank, thinning into the body: wide in the bank's shadow, narrow
		// where the light reaches the shallows.
		float width = edge_width * (1.0 - 0.45 * facing);
		float e = 1.0 - d / width;
		if (d < 1.0 && facing < 0.3) {
			col = tone(TONE_WATERLINE);
		} else if (e > b) {
			col = tone(TONE_EDGE);
		} else if (facing > 0.25 && d < 9.0 && (1.0 - (d - 2.0) / 7.0) * facing > b) {
			// The sunward shallows: where the light reaches over a low bank, a lighter band.
			col = tone(TONE_SHALLOWS);
		} else if (d > 4.0 && (ripple_at(cellpx, ripple_drift_a, vec2(0.0)) || ripple_at(cellpx, ripple_drift_b, vec2(57.0, 23.0)))) {
			col = tone(TONE_RIPPLE);
		}
		COLOR = col;
	}
}
