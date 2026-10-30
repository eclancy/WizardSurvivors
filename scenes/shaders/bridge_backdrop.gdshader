// The Last Bridge's castle on the horizon (scripts/BridgeBackdrop.cs), drawn only over the drop.
//
// The bridge's shape is BridgeBand's (scripts/LevelGenerator.Band.cs): pieces along the bridge, each with a
// half-width to its shore. A pixel whose world point is on the deck, or within `margin` of a shore, is thrown
// away, so the castle is only ever seen across the water - never on the bridge, under an enemy, or over the
// shore line the player reads as the edge, and below the mist line it rises out of. Nothing is blended: a
// pixel is the drawing's or nothing.
shader_type canvas_item;

uniform vec2 origin;
uniform float seg_s[16];
uniform float seg_hw[16];
uniform int seg_count = 0;
uniform float start_s = 0.0;
uniform float end_s = 0.0;
uniform float margin = 28.0;
// The mist the castle rises out of: nothing below this world y, and a dithered band above it.
uniform float mist_y = 1e9;
uniform float mist_band = 10.0;

varying vec2 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

float half_width(float s) {
	if (s < start_s || s >= end_s) {
		return -1e6;
	}
	float hw = 0.0;
	for (int i = 0; i < 16; i++) {
		if (i >= seg_count) {
			break;
		}
		if (s >= seg_s[i]) {
			hw = seg_hw[i];
		}
	}
	return hw;
}

float bayer4(vec2 q) {
	int x = int(mod(q.x, 4.0));
	int y = int(mod(q.y, 4.0));
	int m[16] = int[16](0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5);
	return (float(m[x + y * 4]) + 0.5) / 16.0;
}

void fragment() {
	vec2 cell = floor(world);
	if (cell.y > mist_y - mist_band * bayer4(floor(cell * 0.5))) {
		discard;
	}
	vec2 p = cell + 0.5 - origin;
	float s = (p.x - p.y) * 0.70710678;
	float t = (p.x + p.y) * 0.70710678;
	// Off either end of the bridge, the deck's "width" is negative: everything there is drop.
	if (abs(t) < half_width(s) + margin) {
		discard;
	}
}
