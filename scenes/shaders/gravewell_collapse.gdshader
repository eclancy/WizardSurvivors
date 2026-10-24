// The ground giving way when a Gravewell springs (scripts/GravewellTrap.cs): cracks running out from
// the pit, and a heave of broken earth that travels out to the blast radius and drops there.
//
// THE RADIUS IS THE HIT. `radius` is exactly GravewellTrap's blast radius, the number the damage and
// the root are measured against, and nothing here is drawn past it - not a crack, not a clod. That
// is the whole of what tells the player how far the collapse reached: there is no ring. The heave is
// an animation - a band of clods thrown out for a sixth of a second that falls away on arrival, never
// a line held on the radius, which in a still frame read as a dotted ring - and the cracks are what
// the ground looks like afterwards; they settle, dithering away, through the hold.
//
// Drawn here rather than in _Draw for tentacle_rift.gdshader's reason: the radius is Area-scaled
// continuously, so no sheet at an integer scale could match it, and stamping it square by square
// would be hundreds of draw calls a frame. Every value is computed once per 2 px cell (the game's
// art pixel) and every colour is a Bonelight tone - occlusion, the earth ramp, the gold row's shade
// and deep that the pit's own bank is made of, and the darkness edge. Nothing is blended.
//
// The node's modulate (a Legendary's gold) is kept: the incoming COLOR is multiplied back in, where
// tentacle_rift overrides it.
shader_type canvas_item;
render_mode unshaded;

uniform float radius = 94.0;
// Where the cracks start: inside the pit's bank, which is drawn over them.
uniform float inner = 34.0;
// 0..1: how far the heave has run from the pit to the radius.
uniform float front = 0.0;
// 0..1: the heave falling away once it has reached the radius.
uniform float rubble_fade = 0.0;
// 0..1: the cracks settling through the hold.
uniform float crack_fade = 0.0;
uniform float seed = 0.0;

const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
const vec3 EARTH_HOT = vec3(0.910, 0.769, 0.549);    // earth hot #E8C48C
const vec3 EARTH_MID = vec3(0.788, 0.604, 0.369);    // earth mid #C99A5E
const vec3 EARTH_EDGE = vec3(0.478, 0.314, 0.141);   // earth edge #7A5024
const vec3 GOLD_SHADE = vec3(0.329, 0.247, 0.063);   // gold shade #543F10
const vec3 GOLD_DEEP = vec3(0.180, 0.133, 0.031);    // gold deep #2E2208
const vec3 DARK_EDGE = vec3(0.208, 0.125, 0.361);    // darkness edge #35205C

const float GRID = 2.0;
const vec2 KEY = vec2(-0.70710678, -0.70710678);

varying vec2 local;

void vertex() {
	local = VERTEX;
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

float hash11(float n) {
	return fract(sin(n * 12.9898 + seed * 7.8233) * 43758.5453);
}

// A clod of the bank: mostly the dark soil, now and then a lit one on the key side.
vec3 clod(float k, float lit) {
	if (k > 0.9 && lit > 0.0)
		return EARTH_HOT;
	if (k > 0.72)
		return EARTH_MID;
	if (k > 0.5)
		return EARTH_EDGE;
	return k > 0.3 ? GOLD_SHADE : GOLD_DEEP;
}

void fragment() {
	vec4 tint = COLOR;
	vec2 cell = floor(local / GRID);
	vec2 p = cell * GRID + GRID * 0.5;
	float d = length(p);
	if (d > radius - GRID * 0.5)
		discard;
	float a = atan(p.y, p.x);
	vec2 dir = d > 0.01 ? p / d : vec2(0.0, 1.0);
	float lit = dot(dir, KEY);
	float span = max(GRID, radius - inner);
	float reach = inner + span * front;
	vec3 col = OCC;
	bool hit = false;
	bool crack = false;

	// The cracks: one per sector, wandering sideways as they run out, each at its own angle in its
	// sector so they never fall into an even wheel. Every other one reaches the radius exactly; the
	// rest stop well short, so the edge reads as where the breaking stopped rather than as a ring.
	// Fewer on a wide blast: at Titan Cataclysm's size twenty spokes read as a starburst.
	float n = clamp(floor(radius / 16.0), 7.0, 14.0);
	float sector = floor((a / TAU + 0.5) * n);
	float h = hash11(sector + 1.7);
	float ac = (sector + 0.5 + (h - 0.5) * 0.8) / n * TAU - PI;
	float off = a - ac;
	off = off - TAU * floor((off + PI) / TAU);
	float zig = sin(d * 0.23 + h * 17.0) * 1.6 + sin(d * 0.061 + h * 5.0) * 5.0 * (d / radius);
	float lateral = off * d - zig;
	float len = mod(sector, 2.0) < 0.5 ? radius : inner + span * (0.3 + 0.45 * hash11(sector + 9.1));
	// Three art pixels across at the pit, one at the tip: on a phone at the game's zoom a crack one
	// pixel wide all the way is not there at all.
	float width = mix(GRID * 1.6, GRID * 0.5, clamp((d - inner) / span, 0.0, 1.0));
	vec2 across = vec2(-dir.y, dir.x) * sign(lateral);
	if (d > inner && d < min(len, reach)) {
		if (abs(lateral) < width) {
			col = OCC;
			hit = bayer4(cell) >= crack_fade;
			crack = hit;
		} else if (abs(lateral) < width + GRID && dot(across, KEY) < 0.0) {
			// The wall of the crack that faces back toward the light: one art pixel of lit, torn soil.
			col = EARTH_MID;
			hit = bayer4(cell) >= crack_fade;
		}
	}

	// The heave: a thick band of broken earth thrown out from the pit, densest at its leading edge and
	// thinning behind it, in clods two art pixels across. When it reaches the radius it falls away
	// within a fifth of a second (rubble_fade), so the edge it stops on is a moment, not a line.
	float band = GRID * 7.0;
	if (!hit && d > inner && d <= reach && d > reach - band && rubble_fade < 1.0) {
		vec2 clump = floor(cell / 2.0);
		float k = hash12(clump + floor(front * 9.0) * 0.37);
		float density = mix(0.15, 0.8, (d - (reach - band)) / band) * (1.0 - rubble_fade);
		if (k < density) {
			col = clod(hash12(clump + 7.1), lit);
			hit = true;
		}
	}
	// A breath of the pit's darkness in the near cracks, while the heave is still running.
	if (crack && front < 1.0 && d < inner + span * 0.35 && bayer4(cell) < 0.25)
		col = DARK_EDGE;

	if (!hit)
		discard;
	COLOR = vec4(col, 1.0) * tint;
}
