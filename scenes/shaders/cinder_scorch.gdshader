// A burn on the floor, as pixel art on the game's 2 px grid: Scorching Ray's scorch where it struck
// (ScorchingRayBeam.ScorchMark) and the char under Cinderbreath's embers, walls and backdrafts
// (CinderGround). Both were translucent squashed DrawCircles - a dark wash, doubled where two overlapped -
// with antialiased lines for cracks, which sat badly on a hard-edged, nearest-filtered floor
// (.ai/art-direction.md sections 1 and 3).
//
// One quad per burn, through ScorchGround.Draw, as crater.gdshader's craters are: its numbers ride in the
// vertex colour, so every burn on screen shares one material and batches, and nothing is allocated a frame.
//   r  glow - 1 white-hot cracks, 0 cold char. The cracks cool down Fire's ramp into the red row's coals.
//   g  how much of it is left: 1 whole, 0 gone. It dissolves through the Bayer matrix, edge first.
//   b  its radius / 256 (the quad is radius wide and radius * SQUASH tall either side of its centre).
//   a  its seed, 0..1, so no two burns crack alike.
// Every value is computed once per 2 px cell, and the quad's centre is put on an even world pixel by
// ScorchGround, so the cells are the world's. Colours are opaque Bonelight tones: occlusion, the red row
// for char and cooling coals (crater.gdshader's), robe violet deep for soot, Fire's ramp for the glow.
//
// It is a burn, not a marker: it lies inside the radius it is given and never past it (the ragged edge
// only bites inward), and what tells the reach is the fire standing in it, drawn by its owner.
shader_type canvas_item;
render_mode unshaded;

const float GRID = 2.0;
const float SQUASH = 0.84;

const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
const vec3 RED_LIT = vec3(0.416, 0.125, 0.157);      // red lit #6A2028
const vec3 RED_BASE = vec3(0.290, 0.094, 0.125);     // red base #4A1820
const vec3 RED_SHADE = vec3(0.165, 0.055, 0.078);    // red shade #2A0E14
const vec3 RED_DEEP = vec3(0.086, 0.027, 0.039);     // red deep #16070A
const vec3 VIOLET_DEEP = vec3(0.078, 0.063, 0.122);  // violet deep #14101F
const vec3 FIRE_CORE = vec3(1.000, 0.957, 0.816);    // fire core #FFF4D0
const vec3 FIRE_HOT = vec3(1.000, 0.784, 0.282);     // fire hot #FFC848
const vec3 FIRE_MID = vec3(0.910, 0.478, 0.102);     // fire mid #E87A1A
const vec3 FIRE_EDGE = vec3(0.722, 0.220, 0.102);    // fire edge #B8381A

varying vec2 local;
varying flat vec4 params;

void vertex() {
	params = COLOR;
	float r = COLOR.b * 256.0;
	local = (UV - vec2(0.5)) * 2.0 * vec2(r, r * SQUASH);
}

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

float hash12(vec2 p, float s) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031 + s * 13.7);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float hash11(float n, float s) {
	return fract(sin(n * 12.9898 + s * 78.233) * 43758.5453);
}

// Glow by emission: white-hot down to a dull coal; below that the crack is a cold seam.
vec3 glow(float e) {
	if (e > 0.9) return FIRE_CORE;
	if (e > 0.7) return FIRE_HOT;
	if (e > 0.5) return FIRE_MID;
	if (e > 0.32) return FIRE_EDGE;
	if (e > 0.18) return RED_LIT;
	return RED_BASE;
}

void fragment() {
	float heat = params.r;
	float left = params.g;
	float radius = params.b * 256.0;
	float s = params.a;
	if (radius < GRID * 2.0)
		discard;

	vec2 cell = floor(local / GRID);
	vec2 p = cell * GRID + GRID * 0.5;
	vec2 q = vec2(p.x, p.y / SQUASH);
	float d = length(q) / radius;
	float a = atan(q.y, q.x);
	float b = bayer4(cell);

	// A ragged edge that only ever bites inward, so the burn never lies past its radius.
	float edge = 1.0 - 0.14 * (0.5 + 0.5 * (sin(a * 3.0 + s * 40.0) * 0.6 + sin(a * 7.0 - s * 23.0) * 0.4));
	if (d > edge)
		discard;
	// It goes from the edge in: the outer cells first.
	if (left < b * (0.5 + 0.5 * d) + 0.02)
		discard;

	// The char: darkest at the heart, thinning to scattered soot toward the edge, the floor showing
	// through between - a burn, not a disc.
	float u = d / edge;
	vec3 col = u < 0.4 ? (b < 0.7 ? OCC : RED_DEEP) : (u < 0.7 ? (b < 0.5 ? RED_DEEP : VIOLET_DEEP) : VIOLET_DEEP);
	bool hit = u < 0.7 || (1.0 - u) / 0.3 * 0.8 > b;

	// Cracks running out from the heart, one art pixel wide, glowing while it is hot and cold seams after.
	float cracks = 5.0 + floor(hash11(1.0, s) * 3.0);
	float slot = floor((a + PI) / TAU * cracks);
	float mid = (slot + 0.3 + 0.4 * hash11(slot, s)) / cracks * TAU - PI;
	float reach = 0.55 + 0.4 * hash11(slot + 7.0, s);
	// The crack wanders: its bearing bends a little with distance.
	float bend = 0.18 * sin(u * 9.0 + slot * 2.3 + s * 11.0);
	float off = abs(mod(a - mid - bend + PI, TAU) - PI) * length(q);
	if (u < reach && off <= 1.1) {
		col = heat > 0.05 ? glow(heat * (1.15 - 0.7 * u / reach)) : RED_SHADE;
		hit = true;
	}
	// The coals at its heart, while it is hot.
	if (u < 0.28 && heat > 0.2 && hash12(cell, s) < heat * 0.9) {
		col = glow(heat * (1.0 - u * 1.5));
		hit = true;
	}

	if (!hit)
		discard;
	COLOR = vec4(col, 1.0);
}
