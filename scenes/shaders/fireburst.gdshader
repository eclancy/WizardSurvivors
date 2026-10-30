// A burst of fire in the air (scripts/FireburstLayer.cs): Fireball's detonation and Cluster Bombs'
// bomblets, as pixel art on the game's 2 px grid. One quad per burst, every burst on screen one batch.
//
// Fireball's blast used to be 24 GPU particles - one world px each, a translucent orange no ramp holds,
// thrown at a fixed speed whatever the radius - over a blast 330 px across: the reach the damage tested
// was drawn by nothing at all. Here the fire IS the reach. A white-hot flash at the heart, then a billow
// of fire thrown out from it, hottest along its leading edge, that lands on the radius a sixth of a
// second in and burns out from the middle while it travels; once it has arrived it breaks up by dither
// and is gone in under half a second. An animation of the blast's own fire (.ai/art-direction.md section
// 3, "a one-shot shockwave of the effect's own light, ending on the radius"), never a ring held on it.
//
// THE RADIUS IS THE HIT: the radius in the vertex colour is the one Fireball.Explode tested, and nothing
// is drawn past it - the billow's lumpy edge only ever falls INSIDE it. Every value is computed once per
// 2 px cell of the world and every colour is an opaque Bonelight tone; Bayer dithering is the only
// gradient, as in crater.gdshader, whose flash this one's heart follows.
//
// PER BURST, IN THE VERTEX COLOUR (FireburstLayer.Draw; the node's modulate stays white):
//   r  age / FireburstLayer.LifeSeconds, 0..1
//   g  the radius the hit tested, / 1024
//   b  a seed, 0..1
//   a  the style, (index + 0.5) / 8: 0 fire, 1 Hellfire Nova (down the red rows, with soot), 2 Armageddon
//      Sun (white light at the heart), 3 a Legendary's gold.
// The quad is the burst's bounding square and its UV runs 0..1 across it.
shader_type canvas_item;
render_mode unshaded;

const vec3 FIRE_CORE = vec3(1.000, 0.957, 0.816);    // fire core #FFF4D0
const vec3 FIRE_HOT = vec3(1.000, 0.784, 0.282);     // fire hot #FFC848
const vec3 FIRE_MID = vec3(0.910, 0.478, 0.102);     // fire mid #E87A1A
const vec3 FIRE_EDGE = vec3(0.722, 0.220, 0.102);    // fire edge #B8381A
const vec3 LIGHT_CORE = vec3(1.0, 1.0, 1.0);         // light core #FFFFFF
const vec3 LIGHT_HOT = vec3(1.0, 0.973, 0.847);      // light hot #FFF8D8
const vec3 RED_LIT = vec3(0.416, 0.125, 0.157);      // red lit #6A2028
const vec3 RED_SHADE = vec3(0.165, 0.055, 0.078);    // red shade #2A0E14
const vec3 VIOLET_DEEP = vec3(0.078, 0.063, 0.122);  // violet deep #14101F
const vec3 GOLD_HI = vec3(1.000, 0.941, 0.753);      // gold hi #FFF0C0
const vec3 GOLD_LIT = vec3(0.847, 0.690, 0.290);     // gold lit #D8B04A
const vec3 GOLD_BASE = vec3(0.541, 0.416, 0.118);    // gold base #8A6A1E

const float GRID = 2.0;
// The share of the burst's life the billow takes to reach the radius (0.16 s of 0.42, ImpactDebris's
// travel, so the fire and the debris thrown with it land together).
const float ARRIVE = 0.38;

varying vec2 local;
varying flat vec4 params;
varying flat vec2 centre;

void vertex() {
	params = COLOR;
	float r = COLOR.g * 1024.0;
	local = (UV - vec2(0.5)) * 2.0 * vec2(r);
	centre = VERTEX - local;
}

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

float hash12(vec2 p, float s) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031 + s * 0.0137);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

// Billows: value noise over blobs of a few art pixels, so the fire comes in puffs, not in grain.
float billow(vec2 x, float s) {
	vec2 i = floor(x);
	vec2 f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash12(i, s);
	float b = hash12(i + vec2(1.0, 0.0), s);
	float c = hash12(i + vec2(0.0, 1.0), s);
	float d = hash12(i + vec2(1.0, 1.0), s);
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void fragment() {
	float t = params.r;
	float radius = params.g * 1024.0;
	float s = params.b * 997.0;
	int style = int(params.a * 8.0);

	vec2 cell = floor(local / GRID);
	vec2 p = cell * GRID + GRID * 0.5;
	float d = length(p);
	if (d > radius - GRID * 0.5 || radius < GRID * 4.0)
		discard;
	float b = bayer4(cell);
	float u = d / radius;

	// How far out the billow has been thrown: fast, then coasting onto the radius.
	float k = clamp(t / ARRIVE, 0.0, 1.0);
	float front = radius * (1.0 - (1.0 - k) * (1.0 - k) * (1.0 - k));
	// Its edge is lumpy, but only inward of where it has got to - never past the radius. Measured round
	// the circle on its unit vector, so there is no seam where the angle wraps.
	vec2 dir = d > 0.01 ? p / d : vec2(1.0, 0.0);
	float lump = billow(dir * 3.0 + vec2(s, t * 6.0), s + 5.0);
	float edge = front * (1.0 - 0.1 * lump);
	if (d > edge)
		discard;
	// The fire is a band behind the front, no wider than a body or two however big the blast: at Hellfire
	// Nova's 412 px a blast that filled its disc filled the screen. While the front is still nearer the
	// heart than the band is wide the whole ball burns; after that the middle has burnt out, and the
	// bodies inside the blast are seen again before it has even arrived.
	float band = clamp(radius * 0.24, GRID * 5.0, 56.0);
	float hollow = max(0.0, edge - band);
	float w = clamp((d - hollow) / max(GRID, edge - hollow), 0.0, 1.0);

	// Puffs of fire, blobs a third of the band across, hottest along the leading edge and cooling as the
	// burst ages. The band breaks into separate puffs as it goes, the trailing side first.
	float scale = max(GRID * 3.0, band * 0.3);
	float puff = billow(p / scale + vec2(s, t * 3.0), s);
	float heat = 1.12 - 0.95 * t;
	float e = heat * (0.4 + 0.6 * w) + (puff - 0.5) * 0.55 + (b - 0.5) * 0.1;
	bool gap = puff < (1.0 - w) * 0.55 * clamp(t * 2.2, 0.0, 1.0);

	vec3 col = FIRE_EDGE;
	bool hit = true;
	if (d < hollow || gap) {
		// Just behind the band: a few puffs of smoke and the last of the fire, thinning out. Only just
		// behind it - spread over the whole burnt-out middle they were a screen of speckle.
		float h = hash12(floor(cell / 2.0), s + 9.0);
		if (d < hollow - band)
			hit = false;
		else if (h < 0.18 * (1.0 - t) && style != 2)
			col = h < 0.07 ? RED_SHADE : VIOLET_DEEP;
		else if (h > 0.93 && t < 0.7)
			col = FIRE_EDGE;
		else
			hit = false;
	} else {
		vec3 c0 = style == 2 ? LIGHT_CORE : FIRE_CORE;
		vec3 c1 = style == 2 ? LIGHT_HOT : FIRE_HOT;
		vec3 c2 = style == 1 ? FIRE_EDGE : FIRE_MID;
		vec3 c3 = style == 1 ? RED_LIT : FIRE_EDGE;
		if (style == 3) {
			c1 = GOLD_HI;
			c2 = GOLD_LIT;
			c3 = GOLD_BASE;
		}
		if (e > 0.86)
			col = c0;
		else if (e > 0.64)
			col = c1;
		else if (e > 0.42)
			col = c2;
		else if (e > 0.2)
			col = c3;
		else if (style == 1 && e > 0.05)
			col = puff > 0.5 ? RED_SHADE : VIOLET_DEEP;
		else
			hit = false;
	}

	// The flash: white-hot at the heart for the first instant, shrinking into the billow. Capped in world
	// px, so a wide blast is not a white screen.
	float flash = clamp(1.0 - t / 0.16, 0.0, 1.0);
	float fr = min(radius * 0.32, 30.0) * flash;
	if (d < fr) {
		col = style == 2 ? LIGHT_CORE : FIRE_CORE;
		hit = true;
	} else if (d < fr * 1.4 && b < 0.6) {
		col = style == 3 ? GOLD_HI : (style == 2 ? LIGHT_HOT : FIRE_HOT);
		hit = true;
	}

	// Once it has landed on the radius it breaks up from the outside in, by dither.
	float gone = clamp((t - ARRIVE) / (1.0 - ARRIVE), 0.0, 1.0);
	if (b < gone * 1.15 - (1.0 - u) * 0.25)
		hit = false;

	if (!hit)
		discard;
	COLOR = vec4(col, 1.0);
}
