// The ground under Spiritual Weapon's hammer (scripts/SpiritualWeapon.cs, .ai/spell-roster.md 2f): the
// quake a slam sends through the floor - and Aftershock's second wave - and Hallowed Ground's holy ground.
// On the floor, under every body (Eric, 2026-10-28: "the hammer itself should be above enemies, but the
// quake it creates should be underneath their sprites"); the hammer is in the air, on its own canvas.
//
// The cracks were DrawLines between snapped points - four px of occlusion at 75% under two of Arcane -
// which at any bearing but the four square ones laid a rotated, translucent stroke across the grid, and
// the holy ground's runes were rects faded through alpha. Here every 2 px cell is shaded once, opaque,
// from occlusion, robe violet and the Arcane and Light emissive ramps, Bayer dithering the only gradient
// (.ai/art-direction.md sections 1 and 3), as gravewell_collapse.gdshader draws Gravewell's.
//
// THE RADIUS IS THE HIT. `radius` is the slam's - HammerSlamRadius x every area term, the number the
// blow and the slow are measured against (an Aftershock's is that x AftershockReach) - and the holy
// ground's is the reach that Weakens and slows. Nothing is drawn past it: the cracks run out to it and
// stop; the heave of broken ground races out to it in a sixth of a second and falls away as it arrives
// (an animation, never a line held on the radius); the holy ground's light lies scattered evenly over all
// of it and stops there. Squashed into the floor's three-quarter view as the craters are.
//
// PER QUAD, IN THE VERTEX COLOUR, so every slam and zone on screen shares one material and batches:
//   r  a slam: how far through its life (0..1). Holy ground: how much of it is left (1..0)
//   g  its radius / 512
//   b  its seed, 0..1
//   a  what it is: 0.25 a slam, 0.5 an Aftershock's echo, 1 holy ground
shader_type canvas_item;
render_mode unshaded;

// The spell's evolution / Legendary tint (Player.GetSpellVisualTint), multiplied over every glow.
uniform vec4 tint : source_color = vec4(1.0);

const float GRID = 2.0;
const float SQUASH = 0.84;
const float SLAM_LIFE = 0.7;
const float ECHO_LIFE = 0.45;

const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
const vec3 VIOLET_DEEP = vec3(0.078, 0.063, 0.122);  // robe violet deep #14101F
const vec3 VIOLET_SHADE = vec3(0.133, 0.110, 0.200); // robe violet shade #221C33
const vec3 ARC_CORE = vec3(1.000, 0.941, 1.000);     // arcane core #FFF0FF
const vec3 ARC_HOT = vec3(0.910, 0.753, 1.000);      // arcane hot #E8C0FF
const vec3 ARC_MID = vec3(0.753, 0.549, 1.000);      // arcane mid #C08CFF
const vec3 ARC_EDGE = vec3(0.478, 0.247, 0.831);     // arcane edge #7A3FD4
const vec3 LIGHT_CORE = vec3(1.0, 1.0, 1.0);         // light core #FFFFFF
const vec3 LIGHT_HOT = vec3(1.0, 0.973, 0.847);      // light hot #FFF8D8
const vec3 LIGHT_MID = vec3(1.0, 0.941, 0.659);      // light mid #FFF0A8
const vec3 LIGHT_EDGE = vec3(1.0, 0.776, 0.235);     // light edge #FFC63C

varying vec2 local;
varying flat vec4 params;

void vertex() {
	params = COLOR;
	float r = COLOR.g * 512.0;
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

float wrap_angle(float x) {
	return x - TAU * floor((x + PI) / TAU);
}

// A glyph of three by three cells: which of its nine cells are lit, by its shape number.
bool glyph(int shape, int gx, int gy) {
	int i = (gy + 1) * 3 + (gx + 1);
	// A cross, a saltire, a hook and a ring - marks, not letters.
	int bits = shape == 0 ? 186 : (shape == 1 ? 341 : (shape == 2 ? 457 : 495));
	return ((bits >> i) & 1) == 1;
}

void fragment() {
	float kind = params.a;
	float radius = params.g * 512.0;
	float s = params.b;
	if (radius < GRID * 3.0)
		discard;
	vec2 cell = floor(local / GRID);
	vec2 p = cell * GRID + GRID * 0.5;
	vec2 q = vec2(p.x, p.y / SQUASH);
	float d = length(q);
	float u = d / radius;
	float a = atan(q.y, q.x);
	float b = bayer4(cell);
	if (d > radius - GRID * 0.5)
		discard;
	vec3 col = vec3(0.0);
	bool hit = false;

	if (kind > 0.75) {
		// --- Holy ground: light scattered evenly over the whole of it, twinkling, and runes. Its reach is
		// where the light stops - an even scatter, a little thicker toward the heart, so no band of it
		// gathers on the rim into a ring (a marker; the first build's did). ---
		float left = params.r;
		float cover = min(1.0, left * 3.0);
		float clock = floor(TIME * 7.0 + hash12(cell, s + 2.0) * 5.0);
		float h = hash12(cell + vec2(clock * 5.0, 0.0), s);
		if (h < 0.045 + 0.035 * (1.0 - u)) {
			col = u > 0.7 ? LIGHT_EDGE : (h < 0.015 ? LIGHT_HOT : LIGHT_MID);
			hit = true;
		}
		for (int r = 0; r < 10; r++) {
			float ang = float(r) * TAU / 10.0 + hash11(float(r), s) * 0.3;
			float rr = radius * (0.55 + 0.35 * hash11(float(r) + 17.0, s));
			vec2 c = floor(vec2(cos(ang) * rr, sin(ang) * rr * SQUASH) / GRID);
			ivec2 g = ivec2(cell - c);
			if (abs(g.x) <= 1 && abs(g.y) <= 1 && glyph(int(hash11(float(r) + 31.0, s) * 4.0), g.x, g.y)) {
				col = mod(float(r), 2.0) < 0.5 ? LIGHT_HOT : LIGHT_EDGE;
				hit = true;
			}
		}
		if (hit && cover < b)
			hit = false;
		if (!hit)
			discard;
		COLOR = vec4(col * tint.rgb, 1.0);
	} else {
		// --- A slam, or Aftershock's echo. ---
		bool echo = kind > 0.4;
		float life = echo ? ECHO_LIFE : SLAM_LIFE;
		float t = params.r;
		float age = t * life;
		float grow = min(1.0, age / 0.08);
		float cover = t < 0.5 ? 1.0 : (1.0 - t) / 0.5;

		// The cracks: seven arms out from where it landed, jagged, to exactly the radius it hit - a glowing
		// seam a pixel wide inside a lip of occlusion, white-hot at the heart while it is fresh, cooling to
		// the ramp's edge, then breaking up.
		if (u <= grow) {
			for (int k = 0; k < 7; k++) {
				float base = (float(k) + hash11(float(k), s) * 0.6) * TAU / 7.0;
				float seg = u * 4.0;
				float i0 = floor(seg);
				float w0 = (hash11(float(k) * 5.0 + i0, s + 1.0) - 0.5) * 0.5;
				float w1 = (hash11(float(k) * 5.0 + i0 + 1.0, s + 1.0) - 0.5) * 0.5;
				float bearing = base + mix(i0 == 0.0 ? 0.0 : w0, w1, fract(seg));
				float off = abs(wrap_angle(a - bearing)) * d;
				if (off <= 1.1) {
					col = (t < 0.12 && u < 0.35) ? ARC_CORE : (t < 0.35 ? ARC_HOT : (t < 0.6 ? ARC_MID : ARC_EDGE));
					col *= tint.rgb;
					hit = true;
					break;
				}
				if (off <= (echo ? 2.4 : 3.2)) {
					col = OCC;
					hit = true;
				}
			}
		}
		// The pit it struck: broken ground, its lip glowing while it is fresh. Not an echo's.
		if (!hit && !echo && u < 0.16) {
			col = u > 0.11 && t < 0.4 ? ARC_EDGE * tint.rgb : (b < 0.55 ? OCC : VIOLET_DEEP);
			hit = true;
		}
		if (hit && cover < b)
			hit = false;

		// The heave: a band of broken ground thrown out from the blow to the radius in a sixth of a
		// second, falling away as it arrives.
		if (!hit && age < 0.2) {
			float k = min(1.0, age / 0.16);
			float front = radius * (1.0 - (1.0 - k) * (1.0 - k) * (1.0 - k));
			float band = echo ? 4.0 : 6.0;
			if (d <= front && d > front - band && (1.0 - k * k * k) > b) {
				float h = hash12(cell, s + 9.0);
				if (h > 0.45) {
					col = h > 0.93 ? ARC_MID * tint.rgb : (h > 0.7 ? VIOLET_SHADE : OCC);
					hit = true;
				}
			}
		}

		if (!hit)
			discard;
		COLOR = vec4(col, 1.0);
	}
}
