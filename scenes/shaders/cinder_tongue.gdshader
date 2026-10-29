// One tongue of Cinderbreath's fire (scripts/FlamePuff.cs), as pixel art on the game's 2 px grid.
//
// It was a five-frame sprite strip (tools/art/spell_flames.py) turned to the tongue's heading and scaled
// by the jet's width: turned by the engine its art pixels came out rotated and sheared, and at a width of
// 1.15 or 1.75 they were uneven - the smeared look the Bonelight contract rules out (.ai/art-direction.md
// sections 1 and 6, and spirit_weapons.py's reason for drawing every heading). So, as in
// riptide_wave.gdshader, every fragment snaps itself to the WORLD's 2 px cell first and only then turns
// that cell into the tongue's own frame (x forward along its flight, y across) to decide its colour: the
// art pixels stay square and upright at any heading and any width.
//
// THE HEAD IS THE HIT. A tongue burns what stands within its hit radius of its centre (FlamePuff.HitRadius,
// which the jet's width already scales), so the fire is laid out on that circle: the flame's round front
// is on it, it is eight tenths of it wide either side at its widest, and everything hotter than the
// ramp's mid lies inside it. What streams behind it is the tail - the ramp's mid and edge, then soot,
// thinning through the dither - what the flame leaves, not what burns, as the strip's tail was. Inside,
// fire streams back through it: a coarse value noise on whole art pixels, carried backward along the
// tongue, breaks the heat and licks the edge, so it reads as flame and not as rings.
//
// THE LIFE IS THE RANGE INDICATOR (FlamePuff, unchanged): a tongue is lit for exactly as long as it burns,
// and it lives Lifetime, so a flame that has gone out has stopped hurting. A bud that swells to its full
// head within a few frames, a full tongue, then it tears - cells dropping out through the dither and the
// heat stepping down the ramp - and the last of it is a few embers.
//
// PER TONGUE, IN THE VERTEX COLOUR, so every tongue on screen shares one material and batches:
//   r  how far through its life it is, 0..1
//   g  its hit radius / 128, world px
//   b  a seed, 0..1
// The ramp is a uniform: plain fire, or the element an element path (Hellbreath) turns it to - one
// material per ramp (FlamePuff.MaterialFor), never one per tongue.
shader_type canvas_item;
render_mode unshaded;

uniform vec4 ramp_core : source_color = vec4(1.000, 0.957, 0.816, 1.0); // fire core #FFF4D0
uniform vec4 ramp_hot : source_color = vec4(1.000, 0.784, 0.282, 1.0);  // fire hot #FFC848
uniform vec4 ramp_mid : source_color = vec4(0.910, 0.478, 0.102, 1.0);  // fire mid #E87A1A
uniform vec4 ramp_edge : source_color = vec4(0.722, 0.220, 0.102, 1.0); // fire edge #B8381A

const float GRID = 2.0;
const vec3 VIOLET_DEEP = vec3(0.078, 0.063, 0.122);  // robe violet deep #14101F: soot
const vec3 VIOLET_SHADE = vec3(0.133, 0.110, 0.200); // robe violet shade #221C33

varying vec2 world;
varying flat vec2 origin;
varying flat vec2 axis_x;
varying flat vec2 axis_y;
varying flat vec4 params;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	origin = MODEL_MATRIX[3].xy;
	// Unit axes: every length here is in world px, so the node's scale (the jet's width) is in the
	// radius already and must not be applied twice.
	axis_x = normalize(MODEL_MATRIX[0].xy);
	axis_y = normalize(MODEL_MATRIX[1].xy);
	params = COLOR;
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

// Value noise on a lattice of `size` px, so its blocks are whole art pixels or more.
float vnoise(vec2 p, float size, float s) {
	vec2 g = p / size;
	vec2 i = floor(g);
	vec2 f = fract(g);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash12(i, s);
	float b = hash12(i + vec2(1.0, 0.0), s);
	float c = hash12(i + vec2(0.0, 1.0), s);
	float d = hash12(i + vec2(1.0, 1.0), s);
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

// The tongue's half width at x along it (x forward, the head's front on +R): a round front, widest a
// little ahead of the middle, tapering through the hit circle into a tail behind it.
float envelope(float x, float R, float tail) {
	float widest = R * 0.15;
	if (x > R)
		return 0.0;
	if (x >= widest) {
		float u = (x - widest) / (R - widest);
		return R * 0.8 * sqrt(max(0.0, 1.0 - u * u));
	}
	float u = (widest - x) / (tail + widest);
	return u >= 1.0 ? 0.0 : R * 0.8 * pow(1.0 - u, 1.25);
}

void fragment() {
	float life = params.r;
	float R = params.g * 128.0;
	float s = params.b;
	vec2 cell = floor(world / GRID);
	vec2 c = cell * GRID + GRID * 0.5;
	vec2 rel = c - origin;
	vec2 p = vec2(dot(rel, axis_x), dot(rel, axis_y));
	float b = bayer4(cell);

	// The bud swells to its full size within a few frames; the heat steps down the ramp as it tears.
	float swell = 0.62 + 0.38 * clamp(life / 0.12, 0.0, 1.0);
	float heat = 1.0 - clamp((life - 0.45) / 0.5, 0.0, 1.0);
	float tear = clamp((life - 0.55) / 0.33, 0.0, 1.0);
	float r = R * swell;
	float tail = R * 1.8;

	// It wavers as it flies: the tail swings across, more toward its tip. Fire streams back through it -
	// the noise is carried backward along the tongue - so the heat inside it moves and its edge licks.
	float behind = clamp(-p.x / tail, 0.0, 1.0);
	float sway = sin(p.x * 0.14 + TIME * 17.0 + s * 40.0) * R * 0.2 * behind;
	vec2 q = vec2(p.x, p.y - sway);
	vec2 flow = vec2(q.x + TIME * 90.0, q.y * 1.6);
	float n = vnoise(flow, 7.0, s) * 0.65 + vnoise(flow + vec2(31.0, 17.0), 3.5, s + 1.0) * 0.35;

	vec3 col = vec3(0.0);
	bool hit = false;

	float w = envelope(q.x, r, tail) * (0.78 + 0.4 * n);
	float across = abs(q.y);
	float d = length(p);
	if (across <= w && w > GRID * 0.5) {
		// Hottest just ahead of the middle and along the spine; cooling out to the edge and back down the
		// tail. Broken by the same streaming noise, so it reads as fire and not as rings.
		float spine = 1.0 - across / w;
		float along = q.x >= r * 0.25 ? 1.0 - (q.x - r * 0.25) / (r * 0.75) * 0.45 : 1.0 - (r * 0.25 - q.x) / (tail + r * 0.25);
		float e = spine * clamp(along, 0.0, 1.0) * heat + (n - 0.5) * 0.35;
		int stop = e > 0.6 ? 0 : (e > 0.4 ? 1 : (e > 0.18 ? 2 : 3));
		// Beyond the hit circle it is the tail - what the flame leaves - so nothing there is hotter than
		// the ramp's mid, and its last third is soot.
		if (d > R)
			stop = max(stop, 2);
		col = stop == 0 ? ramp_core.rgb : (stop == 1 ? ramp_hot.rgb : (stop == 2 ? ramp_mid.rgb : ramp_edge.rgb));
		if (behind > 0.7)
			col = b < 0.5 ? ramp_edge.rgb : VIOLET_SHADE;
		if (behind > 0.85)
			col = VIOLET_DEEP;
		// The tail thins toward its tip: whole near the head, half there at the end.
		hit = d <= R || (1.0 - behind * 0.7) > b;
	}

	// It tears: cells drop out through the dither, the edge first, until only embers are left.
	if (hit && tear > 0.0) {
		float keep = 1.0 - tear * (0.55 + 0.6 * clamp(d / R, 0.0, 1.0));
		if (keep < b)
			hit = false;
	}
	// The embers it leaves: a few cells inside the head, drifting back, glowing as they go out.
	if (!hit && life > 0.5 && d <= R - 1.0) {
		vec2 drift = floor(vec2(p.x + life * R * 0.6, p.y) / GRID);
		float h = hash12(drift, s + 3.0);
		if (h > 0.94 - 0.05 * tear) {
			col = h > 0.985 ? ramp_hot.rgb : (heat > 0.3 ? ramp_mid.rgb : ramp_edge.rgb);
			hit = true;
		}
	}

	if (!hit || life >= 1.0)
		discard;
	COLOR = vec4(col, 1.0);
}
