// What Shadow Bolt leaves on the ground (scripts/ShadowCurse.cs): Creeping Rot's paths and Anathema's
// palls of night, as pixel art on the game's 2-unit grid.
//
// They were stacks of translucent antialiased DrawLines and DrawCircles - a path three lines and a
// handful of fading circles, a pall seven overlapping translucent discs, squashed - the smooth washes
// the Bonelight contract rules out. Each is one quad now, and as in void_scar.gdshader every fragment
// snaps to the WORLD's 2-unit cell first and is then measured in the quad's own frame. Opaque tones only
// - the Poison ramp for the rot (Shadow Bolt's poison), the Darkness ramp and robe violet's deep and
// occlusion for the night - with Bayer dithering the only gradient: a mottled band, a pool that thins
// toward its edge, a fade in and out that dissolves rather than turns translucent.
//
// THE REACH IS THE CURSE'S. A path is drawn out to `reach` (ShadowCurse.PathHalfWidth) from its line,
// round its ends too, which is exactly the distance ShadowCurse.CurseWhatStands measures; a pall to
// `reach`, its radius - a circle, as the curse is, where it used to be drawn squashed. The rim is the
// stuff itself thinning out, never a ring round it.
shader_type canvas_item;
render_mode unshaded;

// 0 a path of rot along x from 0 to `path_length`, 1 a pall round the origin.
uniform int mode = 0;
uniform float path_length = 100.0;
uniform float reach = 16.0;
// 0..1: in, held, and out, dissolving at both ends of its life.
uniform float strength = 1.0;
uniform float time = 0.0;
uniform float seed = 0.0;

const float GRID = 2.0;
const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
const vec3 ROBE_DEEP = vec3(0.078, 0.063, 0.122);    // robe violet deep #14101F
const vec3 DARK_HOT = vec3(0.706, 0.549, 0.910);     // darkness hot #B48CE8
const vec3 DARK_MID = vec3(0.541, 0.361, 0.769);     // darkness mid #8A5CC4
const vec3 DARK_EDGE = vec3(0.208, 0.125, 0.361);    // darkness edge #35205C
const vec3 ROT_HOT = vec3(0.863, 0.957, 0.549);      // poison hot #DCF48C
const vec3 ROT_MID = vec3(0.706, 0.878, 0.290);      // poison mid #B4E04A
const vec3 ROT_EDGE = vec3(0.420, 0.580, 0.125);     // poison edge #6B9420
const vec2 KEY = vec2(-0.70710678, -0.70710678);

varying vec2 world;
varying flat vec2 origin;
varying flat vec2 axis_x;
varying flat vec2 axis_y;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	origin = MODEL_MATRIX[3].xy;
	axis_x = normalize(MODEL_MATRIX[0].xy);
	axis_y = normalize(MODEL_MATRIX[1].xy);
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

// A path of rot: darkest down its spine, mottled rot toward its edge, a broken seam of the rot's light
// down the middle and bubbles working up through it. d is the distance from its line.
bool rot(vec2 p, vec2 cell, float b, out vec3 col) {
	col = ROBE_DEEP;
	float u = clamp(p.x, 0.0, path_length);
	float d = length(p - vec2(u, 0.0));
	// A ragged edge, an art pixel or two in from the reach here and there - never out past it.
	float rag = hash12(vec2(floor(u / 6.0), sign(p.y) * 3.0)) > 0.6 ? GRID : 0.0;
	float edge = reach - rag;
	if (d > edge)
		return false;
	float t = d / max(GRID, edge);
	if (t > 0.62) {
		col = ROT_EDGE;
		return (1.0 - t) * 1.4 + 0.25 > b;
	}
	if (d < GRID * 0.75 && hash12(vec2(floor(u / 8.0), 9.0)) > 0.25) {
		col = ROT_MID;
		return true;
	}
	// Bubbles: a cell of the rot's light swelling and gone, here and there.
	vec2 bub = floor(p / (GRID * 3.0));
	float h = hash12(bub + 41.0);
	float beat = fract(time * 1.3 + h);
	if (h > 0.86 && beat < 0.35) {
		col = beat < 0.15 ? ROT_HOT : ROT_MID;
		return true;
	}
	col = t * 1.6 > b ? DARK_EDGE : (t > 0.2 ? ROBE_DEEP : OCC);
	return true;
}

// A pall of night: a pool that is densest at its heart and thins toward its rim, its rim drifting in
// lobes that turn slowly - inside the radius, never past it - and motes of the Darkness's light lifting
// off it.
bool pall(vec2 q, vec2 cell, float b, out vec3 col) {
	col = OCC;
	float d = length(q);
	if (d > reach)
		return false;
	float a = atan(q.y, q.x);
	float lobes = 0.5 + 0.5 * sin(a * 7.0 + time * 0.4 + seed) * sin(a * 3.0 - time * 0.3);
	float rim = reach * (0.86 + 0.14 * lobes);
	if (d > rim)
		return false;
	float t = d / rim;
	vec2 dir = d > 0.01 ? q / d : vec2(0.0, -1.0);
	float lit = dot(dir, KEY);
	if (rim - d < GRID * 1.5) {
		col = lit > 0.35 ? DARK_MID : DARK_EDGE;
		return 0.7 > b;
	}
	vec2 rising = floor((q + vec2(0.0, time * 9.0)) / GRID);
	float h = hash12(rising + 17.0);
	if (h > 0.985) {
		col = h > 0.995 ? DARK_HOT : DARK_MID;
		return true;
	}
	// Swirls of the Darkness's edge turning in it.
	float swirl = fract((a + t * 3.0 - time * 0.6) * 5.0 / 6.2831853);
	if (t > 0.25 && t < 0.85 && swirl < 0.12) {
		col = DARK_EDGE;
		return true;
	}
	col = t < 0.5 ? OCC : ROBE_DEEP;
	return (1.0 - t) * 1.15 + 0.25 > b;
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	vec2 rel = centre - origin;
	vec2 p = vec2(dot(rel, axis_x), dot(rel, axis_y));
	float b = bayer4(cell);
	vec3 col;
	bool hit = mode == 1 ? pall(p, cell, b, col) : rot(p, cell, b, col);
	// In and out of being as a dissolve, in a different order from the dither inside it.
	if (!hit || strength < bayer4(cell + vec2(2.0, 1.0)))
		discard;
	COLOR = vec4(col, 1.0);
}
