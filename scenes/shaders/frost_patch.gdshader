// Glacial Spike's Rimefoot (scripts/FrostPatch.cs): the ground a spike erupted from, left frozen for a
// few seconds. One quad over the patch; every 2 px cell (the game's art pixel) is shaded once, opaque
// Bonelight tones, Bayer dithering the only gradient (as mire_surface.gdshader and water_surface do).
//
// THE RADIUS IS THE SLOW. `radius` is FrostPatch.Radius, the reach its slow tests against an enemy's
// centre - the spike's own hit radius. The frost's edge wanders, but only INWARD of it (at most 8%), so
// the ice never promises more ground than it slows. No ring, no rim line (.ai/spell-roster.md section
// 2c): what reads as the edge is where the rime stops.
//
// What it is: a crust of rime over the floor, never a solid disc - cells of frost thinning out toward
// the edge so the floor still shows through, the deep ice blue under them, cracks running out from the
// hole the spike went back into, and a few crystals catching the light. It creeps out from the hole in
// its first instant and, at the end, thaws from the edge inward, dithered rather than faded.
shader_type canvas_item;
render_mode unshaded;

uniform vec2 centre = vec2(0.0);
uniform float radius = 56.0;
uniform float age = 0.0;
uniform float life = 3.0;
uniform float seed = 0.0;

const float GRID = 2.0;
const float GROW_SECONDS = 0.18;
const float THAW_SECONDS = 0.6;

// The Ice ramp (.ai/art-direction.md section 3) and the shared occlusion.
const vec3 OCC = vec3(0.020, 0.027, 0.047);      // #05070C - in the cracks
const vec3 EDGE = vec3(0.180, 0.498, 0.769);     // ice edge #2E7FC4 - the deep ice
const vec3 MID = vec3(0.498, 0.863, 0.973);      // ice mid #7FDCF8 - the rime
const vec3 HOT = vec3(0.784, 0.957, 1.0);        // ice hot #C8F4FF - frost catching the light
const vec3 CORE = vec3(0.957, 0.996, 1.0);       // ice core #F4FEFF - a crystal's glint

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

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 p = (cell + 0.5) * GRID - centre;
	float d = length(p) / max(radius, 1.0);
	float a = atan(p.y, p.x);

	// The edge wanders inward only, so the frost never reaches past the slow.
	float bank = 1.0 - 0.08 * (0.5 + 0.5 * sin(a * 5.0 + seed) * sin(a * 3.0 - seed * 1.7));
	float grow = clamp(age / GROW_SECONDS, 0.0, 1.0);
	grow = 1.0 - (1.0 - grow) * (1.0 - grow);
	float edge = bank * mix(0.25, 1.0, grow);
	if (d > edge) {
		discard;
	}

	float b = bayer4(cell);
	float n = hash12(cell + seed * 13.0);
	// Thawing: from the edge inward, cell by cell.
	float thaw = clamp((age - (life - THAW_SECONDS)) / THAW_SECONDS, 0.0, 1.0);
	if (thaw > 0.0 && b * 0.5 + (1.0 - d / edge) * 0.6 < thaw * 1.1) {
		discard;
	}

	// The cracks: six run out from the hole, each a wandering line one cell wide.
	float cracks = 0.0;
	for (int k = 0; k < 6; k++) {
		float ang = seed * 2.3 + float(k) * 1.0472 + 0.35 * sin(seed + float(k) * 4.1);
		float wander = ang + 0.12 * sin(d * 9.0 + float(k));
		vec2 ray = vec2(cos(wander), sin(wander));
		float along = dot(p, ray);
		float across = abs(p.x * ray.y - p.y * ray.x);
		float reach = radius * (0.55 + 0.35 * fract(sin(float(k) * 12.9898 + seed) * 43758.5453));
		if (along > 0.0 && along < reach && across < GRID * 0.5) {
			cracks = 1.0;
		}
	}

	// The hole the spike withdrew into, the cracks, and a crust that thins out toward the edge. (Godot
	// forbids a return in fragment(), so the three are one chain.)
	float hole = radius * 0.12;
	float cover = mix(0.92, 0.28, smoothstep(0.15, 1.0, d / edge));
	vec3 col;
	if (length(p) < hole) {
		col = OCC;
	} else if (cracks > 0.5) {
		col = d < 0.3 ? OCC : EDGE;
	} else {
		if (b > cover) {
			discard;
		}
		col = n < 0.55 ? EDGE : (n < 0.88 ? MID : HOT);
		// The lip of the frost toward the key light (upper left) catches it.
		if (d > edge - 0.1 && dot(normalize(p), vec2(-0.70710678, -0.70710678)) > 0.3) {
			col = HOT;
		}
		if (n > 0.985) {
			col = CORE;
		}
	}
	COLOR = vec4(col, 1.0);
}
