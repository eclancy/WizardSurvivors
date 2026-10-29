// Magic Missile (scripts/MagicMissile.cs): the dart and Arcane Orb, as pixel art on the game's 2-unit
// grid.
//
// The dart was a sprite turned to its heading at half its authored scale, so its art pixels were half
// the world's and stood on a rotated grid, under a GPUParticles2D spray of translucent teal squares
// spinning as they went; the orb was translucent DrawCircles under an antialiased DrawArc. As in
// riptide_wave.gdshader, every fragment here snaps to the WORLD's 2-unit cell first and is then measured
// in the dart's own frame (x forward, y across), so the art pixels stay square and upright however it
// turns as it homes. Opaque Arcane tones only (.ai/art-direction.md section 3: a white-hot core, the
// identity in mid and edge), and Bayer dithering the only gradient.
//
// THE DART keeps the drawn one's silhouette (tools/art/spell_fx.py magic_missile: "the SOLID shape - a
// filled head with a tail behind it and no hole anywhere in it", told apart from Chain Lightning's
// open line and Shadow Bolt's ring) at the size it was drawn on screen: a pointed head `head` long, a
// wavering tail behind, and the spray that was the particles now sparks shed off the tail, in the
// dart's frame so they stream behind it. `size` scales the whole of it with the node's (an evolution's)
// scale.
//
// THE ORB is drawn out of the radius it grinds (MagicMissile.OrbHitRadius): a lit rim on that radius,
// a body dithered at a third, so the bodies being ground show through the gaps rather than through a
// translucent wash, the light on the key's side (upper left, section 2) and five motes turning inside.
// It swells and flickers in its last fifth, before the burst: the swelling thing itself is its warning.
//
// The node's modulate (an evolution's card tint) is overridden, as in tentacle_rift.gdshader: a
// multiply over this ramp only muddies it. A Legendary's gold is the `gilded` rim.
shader_type canvas_item;
render_mode unshaded;

// 0 the dart, 1 the orb.
uniform int mode = 0;
uniform float size = 1.0;
uniform float time = 0.0;
uniform float seed = 0.0;
// The orb: its radius in world units, and how far into its last fifth it is (0..1).
uniform float orb_radius = 26.0;
uniform float orb_end = 0.0;
uniform bool gilded = false;

const float GRID = 2.0;
const vec3 ARC_CORE = vec3(1.0, 0.941, 1.0);         // arcane core #FFF0FF
const vec3 ARC_HOT = vec3(0.910, 0.753, 1.0);        // arcane hot #E8C0FF
const vec3 ARC_MID = vec3(0.753, 0.549, 1.0);        // arcane mid #C08CFF
const vec3 ARC_EDGE = vec3(0.478, 0.247, 0.831);     // arcane edge #7A3FD4
const vec3 ROBE_SHADE = vec3(0.133, 0.110, 0.200);   // robe violet shade #221C33
const vec3 GOLD_LIT = vec3(0.847, 0.690, 0.290);     // gold lit #D8B04A
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

// The dart, in its own frame and world units. x forward from the dart's centre, y across.
bool dart(vec2 p, float b, out vec3 col) {
	col = ARC_EDGE;
	float s = size;
	float tip = 13.0 * s;
	float shoulder = -1.0 * s;
	float hh;
	if (p.x > tip || p.x < -30.0 * s)
		return false;
	if (p.x >= shoulder) {
		// The head: a long point, widest at the shoulder.
		hh = 5.5 * s * pow(clamp((tip - p.x) / (tip - shoulder), 0.0, 1.0), 0.85);
		float a = abs(p.y);
		if (a > hh)
			return false;
		float inner = hh - a;
		vec2 core = vec2(4.0 * s, 0.0);
		float pulse = 1.0 + 0.12 * sin(time * 14.0);
		float dc = length((p - core) * vec2(0.8, 1.15));
		if (dc < 1.6 * s * pulse)
			col = ARC_CORE;
		else if (dc < 3.6 * s * pulse)
			col = ARC_HOT;
		else if (inner > GRID * 0.9)
			col = ARC_MID;
		else
			col = gilded ? GOLD_LIT : ARC_EDGE;
		if (p.x > tip - GRID * 1.5)
			col = ARC_CORE;
		return true;
	}
	// The tail: a wavering ribbon thinning back from the shoulder, its centre swaying.
	float back = (shoulder - p.x) / (13.0 * s);
	if (back <= 1.0) {
		float sway = sin(time * 18.0 - back * 5.0) * 1.6 * s * back;
		hh = 3.6 * s * (1.0 - back * 0.8);
		float a = abs(p.y - sway);
		if (a <= hh) {
			col = a < hh * 0.45 && back < 0.5 ? ARC_MID : ARC_EDGE;
			// Thinning to a dither at its end.
			return back < 0.55 || (1.0 - back) * 2.2 > b;
		}
	}
	// The spray that was the particles: sparks shed off the tail, streaming back from the dart.
	if (back > 0.15 && abs(p.y) < 6.0 * s) {
		vec2 drift = floor(vec2(p.x + time * 60.0, p.y) / GRID);
		float h = hash12(drift + vec2(floor(time * 12.0) * 7.0, 0.0));
		float density = 0.11 * (1.0 - clamp((back - 0.15) / 1.2, 0.0, 1.0));
		if (h > 1.0 - density) {
			col = h > 1.0 - density * 0.3 ? ARC_HOT : ARC_MID;
			return true;
		}
	}
	return false;
}

// The orb, in world units round its centre: a sphere lit from the key, dithered to let what it grinds
// show through.
bool orb(vec2 q, float b, vec2 key, out vec3 col) {
	col = ARC_EDGE;
	float r = orb_radius * (1.0 + 0.12 * orb_end);
	float d = length(q);
	if (d > r)
		return false;
	float flicker = orb_end > 0.0 ? step(0.0, sin(time * 60.0)) : 1.0;
	vec2 dir = d > 0.01 ? q / d : vec2(0.0, -1.0);
	float lit = dot(dir, key);
	if (r - d < GRID) {
		col = lit > 0.25 ? (gilded ? GOLD_LIT : ARC_MID) : ARC_EDGE;
		return true;
	}
	// The light on the key's side, round a white-hot heart, breathing as it nears its end.
	vec2 hl = q - key * r * 0.32;
	float dh = length(hl);
	if (dh < r * 0.16) {
		col = ARC_CORE;
		return true;
	}
	if (dh < r * 0.34 && flicker > 0.5) {
		col = ARC_HOT;
		return true;
	}
	if (dh < r * 0.6) {
		col = ARC_MID;
		return 0.62 > b;
	}
	// Five motes turning inside.
	for (int k = 0; k < 5; k++) {
		float a = time * 3.2 + 6.2831853 * float(k) / 5.0;
		vec2 m = vec2(cos(a), sin(a) * 0.8) * r * 0.62;
		if (length(q - m) < GRID * 1.1) {
			col = ARC_CORE;
			return true;
		}
	}
	// The body: a third of it, so the ground and the bodies under it show through.
	if (r - d < GRID * 2.0) {
		col = ARC_EDGE;
		return 0.7 > b;
	}
	col = d < r * 0.8 ? ARC_EDGE : ROBE_SHADE;
	return 0.3 > b;
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	vec2 rel = centre - origin;
	float b = bayer4(cell);
	vec3 col;
	bool hit;
	if (mode == 1) {
		// The orb does not turn with its heading: it is lit from the upper left wherever it rolls.
		hit = orb(rel, b, KEY, col);
	} else {
		vec2 p = vec2(dot(rel, axis_x), dot(rel, axis_y));
		hit = dart(p, b, col);
	}
	if (!hit)
		discard;
	COLOR = vec4(col, 1.0);
}
