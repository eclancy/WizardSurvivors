// The Riptide wave (scripts/RiptideWave.cs): a small breaking wave travelling along the bolt's
// heading, as pixel art on the game's 2 px grid.
//
// The bolt is rotated to its heading, so a sprite or a stroked shape would put its pixels on a
// rotated grid - the smooth, vector look the Bonelight contract rules out, and what made the first
// drawn wave read as a blue crescent blade. So every fragment snaps itself to the WORLD's 2 px cell
// first and only then turns that cell into the wave's own frame (x forward, y across) to decide its
// colour. The art pixels stay square and upright whatever the heading, and every colour is a Water
// or azure tone (.ai/art-direction.md section 3) with Bayer dithering as the only gradient, as in
// water_surface.gdshader and tentacle_rift.gdshader.
//
// SIZE: `half_width` is the swath the bolt cuts, in the node's own units (RiptideWave.SwathHalfWidth);
// nothing is drawn wider than it across the heading, and only the trailing spray and wake reach
// behind the body. The titanic form (Titan Cataclysm, and so Spring Tide) is heavier and darker in
// the same band, with spume heaped up the screen off its lip.
shader_type canvas_item;
render_mode unshaded;

uniform float half_width = 20.0;
uniform float time = 0.0;
uniform float seed = 0.0;
uniform bool titanic = false;
// The screen's "up", in the wave's frame - the titanic spume heaps toward it.
uniform vec2 up_local = vec2(0.0, -1.0);
// A legendary roll: the lip's foam is gold.
uniform bool gilded = false;
// An element path (Brackish Tide, RiptideWave.cs): the wave turns toward that element's ramp. The
// foam takes the element's own core and hot; the face, body and trough keep enough of the sea to
// read as water gone that colour rather than as a different thing.
uniform bool has_path = false;
uniform vec4 path_core : source_color = vec4(1.0);
uniform vec4 path_hot : source_color = vec4(1.0);
uniform vec4 path_mid : source_color = vec4(0.5);
uniform vec4 path_edge : source_color = vec4(0.25);

const vec3 CORE = vec3(0.941, 0.980, 1.0);       // water core #F0FAFF
const vec3 HOT = vec3(0.706, 0.863, 1.0);        // water hot #B4DCFF
const vec3 MID = vec3(0.435, 0.722, 0.941);      // water mid #6FB8F0
const vec3 EDGE = vec3(0.165, 0.373, 0.769);     // water edge #2A5FC4
const vec3 AZ_BASE = vec3(0.165, 0.447, 0.847);  // azure base #2A72D8
const vec3 AZ_SHADE = vec3(0.102, 0.243, 0.541); // azure shade #1A3E8A
const vec3 AZ_DEEP = vec3(0.055, 0.118, 0.282);  // azure deep #0E1E48
const vec3 GOLD_HI = vec3(1.0, 0.941, 0.753);    // gold hi #FFF0C0
const float GRID = 2.0;

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

// The leading edge of the crest at s (-1..1 across): bulging forward in the middle, the horns swept
// back, with a ripple travelling along it as the lip rolls over and a lumpy edge of foam.
float front_at(float s, float r, float phase) {
	float swell = 1.0 - s * s;
	float lump = hash12(vec2(floor(s * r / GRID), floor(time * 10.0))) - 0.5;
	return r * (-0.14 + 0.52 * pow(max(swell, 0.0), 0.8)) + r * 0.05 * sin(phase + s * 3.1) + lump * GRID * 1.2;
}

void fragment() {
	// The world's art pixel, then where its centre sits in the wave's frame.
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	mat2 m = mat2(axis_x, axis_y);
	vec2 p = inverse(m) * (centre - origin);

	// The palette for this wave: the sea's, or the sea turned toward its element path.
	vec3 c_core = CORE;
	vec3 c_hot = HOT;
	vec3 c_mid = MID;
	vec3 c_edge = EDGE;
	vec3 c_shade = AZ_SHADE;
	vec3 c_deep = AZ_DEEP;
	if (has_path) {
		c_core = path_core.rgb;
		c_hot = path_hot.rgb;
		c_mid = mix(MID, path_mid.rgb, 0.65);
		c_edge = mix(EDGE, path_edge.rgb, 0.6);
		c_shade = mix(AZ_SHADE, path_edge.rgb * 0.55, 0.6);
		c_deep = mix(AZ_DEEP, path_edge.rgb * 0.25, 0.6);
	}

	float r = half_width;
	float phase = time / 0.42 * 6.28318530718 + seed;
	float lip_scale = titanic ? 1.6 : 1.0;
	vec3 col = vec3(0.0);
	bool hit = false;

	// The titanic spume, heaped off the lip toward the top of the screen. Tested first because it
	// rises over the crest; it never leaves the band across the heading.
	if (titanic) {
		for (int k = 0; k < 6; k++) {
			float s = -0.75 + 1.5 * float(k) / 5.0;
			float swell = 1.0 - s * s;
			float rise = r * (0.16 + 0.17 * (0.5 + 0.5 * sin(phase * 0.8 + float(k) * 1.7))) * swell;
			vec2 foot = vec2(front_at(s, r, phase) - r * 0.08, s * r);
			vec2 top = foot + up_local * rise;
			float size = max(GRID * 1.2, r * 0.13 * (0.7 + 0.3 * swell));
			if (abs(top.y) > r)
				continue;
			if (length(p - top) < size) { col = hash12(cell) > 0.3 ? c_core : c_hot; hit = true; break; }
			if (length(p - mix(foot, top, 0.5)) < size * 0.85) { col = c_hot; hit = true; break; }
		}
	}

	float s = p.y / r;
	if (!hit && abs(s) <= 1.0) {
		float swell = 1.0 - s * s;
		float front = front_at(s, r, phase);
		float lip = r * (0.06 + 0.12 * swell) * lip_scale * (0.85 + 0.15 * sin(phase)) + GRID;
		float face = r * (0.06 + 0.22 * swell);
		float back = front - r * (0.34 + 0.32 * swell);
		// The ends of the crest do not taper to points - a tapering crescent is a blade. They break
		// up into foam and spray, so the wave dissolves at its edges the way water does.
		float ragged = smoothstep(0.62, 1.0, abs(s));
		float slope = back - r * (0.05 + 0.30 * pow(max(swell, 0.0), 0.7));
		float x = p.x;

		if (ragged > bayer4(cell + vec2(floor(time * 8.0), 0.0)) && x <= front) {
			if (x > front - lip * 1.6 && hash12(cell + floor(time * 12.0)) > 0.55) { col = hash12(cell) > 0.5 ? c_core : c_hot; hit = true; }
		} else if (x > front) {
			// Ahead of the crest: only the titanic wave's trough, the sea drawn down before it breaks.
			if (titanic && x < front + r * 0.20 * swell) { col = c_deep; hit = true; }
		} else if (x > front - lip) {
			// The lip: foam, never quite the same two frames running.
			float h = hash12(cell + floor(time * 14.0));
			col = (x > front - GRID || h > 0.55) ? c_core : c_hot;
			if (h < 0.08) col = c_mid;
			if (gilded && h > 0.8) col = GOLD_HI;
			hit = true;
		} else if (x > front - lip - GRID) {
			// The shadow under the curl.
			col = titanic ? vec3(0.020, 0.027, 0.047) : c_deep;
			hit = true;
		} else if (x > front - lip - GRID - face) {
			// The face: lit water with streaks dragged back along it.
			float band = floor(p.y / GRID);
			float streak = fract(x * 0.07 + hash12(vec2(band, 3.0)) * 4.0 + time * 2.5);
			col = titanic ? c_edge : c_mid;
			if (streak < 0.18) col = titanic ? c_mid : c_hot;
			hit = true;
		} else if (x > back) {
			// The body of the wave, darkening toward its back.
			float t = (front - lip - face - x) / max(GRID, front - lip - face - back);
			col = titanic ? c_shade : c_edge;
			col = t > bayer4(cell) ? (titanic ? c_deep : c_shade) : col;
			hit = true;
		} else if (x > slope) {
			// The back slope, half there: a dither of deep water over the ground.
			if (bayer4(cell) < (titanic ? 0.6 : 0.45)) { col = titanic ? c_deep : c_edge; hit = true; }
		}

		// Spray and wake, behind the crest only.
		if (!hit && x <= front) {
			int drops = titanic ? 18 : 9;
			for (int k = 0; k < 18; k++) {
				if (k >= drops) break;
				float fk = float(k);
				float c = fract(time * 1.9 + fk / float(drops) + seed);
				float ds = (hash12(vec2(fk, 7.0)) * 2.0 - 1.0) * 0.85;
				float reach = (0.7 + 0.55 * hash12(vec2(fk, 11.0))) * (titanic ? 1.5 : 1.0);
				vec2 d = vec2(front_at(ds, r, phase) - r * 0.2 - c * r * reach, clamp(ds * r * (1.0 + 0.06 * c), -r, r));
				if (titanic) d += up_local * sin(c * 3.14159) * r * 0.35;
				float size = max(GRID * 0.75, r * 0.11 * (1.0 - 0.6 * c));
				if (length(p - d) < size && c < 0.85) { col = mod(fk, 3.0) < 1.0 ? c_core : c_hot; hit = true; break; }
			}
			if (!hit && titanic && abs(s) < 0.85 && x < slope) {
				float lane = floor((s + 1.0) * 1.5);
				float dash = fract(-x / (r * 0.45) + time * 2.4 + lane * 0.37);
				if (dash < 0.35 && x > slope - r * 1.4 && abs(fract((s + 1.0) * 1.5) - 0.5) < 0.12) { col = c_hot; hit = true; }
			}
		}
	}

	if (!hit)
		discard;
	COLOR = vec4(col, 1.0);
}
