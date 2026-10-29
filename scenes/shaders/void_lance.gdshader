// Void Lance (scripts/VoidLance.cs): a lance of void thrust out of the wizard's hands, as pixel art on
// the game's 2 px grid.
//
// It was a Line2D with a smooth violet gradient under a Polygon2D with vertex colours, both
// translucent and antialiased, and forty Sprite2D motes a second: at any angle but the four square
// ones it was the smooth vector look the Bonelight contract rules out, and Witching Hour's hand, which
// never stops turning, smeared it into a soft violet blur. So, as in riptide_wave.gdshader, every
// fragment snaps itself to the WORLD's 2 px cell first and only then turns that cell into the lance's
// own frame (x out along it from the wizard, y across) to decide its colour. The art pixels stay square
// and upright whatever the lance's angle and however Witching Hour turns it. Every colour is opaque and
// a Bonelight tone (.ai/art-direction.md section 3): the Arcane ramp is the lance's light - a white-hot
// filament down its spine and a needle of it at the tip - and the Darkness ramp, robe violet's deep
// and occlusion its body, so it reads as a blade of void that has light in it rather than as a beam.
// Bayer dithering is the only gradient; nothing is blended and nothing is antialiased.
//
// THE BLADE IS THE HIT. Every length arrives in world px from the numbers VoidLance's hit test uses:
// `reach` is HitLength, `back_reach` Void Glaive's blade behind (the same line run on past the wizard,
// VoidLance.Butt), `half_width` the blade's half width and `hit_radius` HitRadius - the blade and the
// body either side of it that still counts as touching it. The blade is drawn at full width along its
// length; the motes it sheds drift in the body's allowance, never past HitRadius; and Cosmic Collapse's
// hole is drawn at the radius its burst strikes, swelling to it and caving in. Nothing marks the reach:
// the blade, the motes and the hole are the reach (section 3, "The player's spells carry no area
// markers"). The one thing drawn outside the line is Witching Hour's smear: the ground the hand has
// just swept, broken into streaks behind it as in stormsweep_lash.gdshader - speed, not a fill.
//
// The node's modulate is overridden, as in tentacle_rift.gdshader: Cosmic Collapse's card tint
// (0.3, 0.1, 0.7) multiplied over this ramp turned the whole lance to mud. A Legendary's gold is the
// `gilded` lip instead.
shader_type canvas_item;
render_mode unshaded;

uniform float reach = 280.0;
uniform float back_reach = 0.0;
uniform float half_width = 9.0;
uniform float hit_radius = 15.0;
uniform float time = 0.0;
uniform float seed = 0.0;
// 1 on the frame the lance is cast, cooling to 0 over a tenth of a second: the thrust's flash.
uniform float heat = 0.0;
// 0..1 as the lance withdraws: it dissolves through the Bayer matrix.
uniform float fade = 0.0;
// Witching Hour: how far behind the turning hand its smear reaches, in radians. 0 for a thrust.
uniform float trail = 0.0;
// Cosmic Collapse: the hole's radius at this moment, in world px, and how far through its collapse it
// is (0..1). A negative hole_t means there is no hole.
uniform float hole_radius = 0.0;
uniform float hole_t = -1.0;
uniform bool gilded = false;

const float GRID = 2.0;
// Inside the wizard's own sprite nothing is drawn: the blade starts at his hands.
const float HAND = 8.0;

const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
const vec3 ARC_CORE = vec3(1.0, 0.941, 1.0);         // arcane core #FFF0FF
const vec3 ARC_HOT = vec3(0.910, 0.753, 1.0);        // arcane hot #E8C0FF
const vec3 ARC_MID = vec3(0.753, 0.549, 1.0);        // arcane mid #C08CFF
const vec3 ARC_EDGE = vec3(0.478, 0.247, 0.831);     // arcane edge #7A3FD4
const vec3 DARK_HOT = vec3(0.706, 0.549, 0.910);     // darkness hot #B48CE8
const vec3 DARK_MID = vec3(0.541, 0.361, 0.769);     // darkness mid #8A5CC4
const vec3 DARK_EDGE = vec3(0.208, 0.125, 0.361);    // darkness edge #35205C
const vec3 ROBE_DEEP = vec3(0.078, 0.063, 0.122);    // robe violet deep #14101F
const vec3 GOLD_HI = vec3(1.0, 0.941, 0.753);        // gold hi #FFF0C0
const vec3 GOLD_LIT = vec3(0.847, 0.690, 0.290);     // gold lit #D8B04A
// The key light, from the upper left (.ai/art-direction.md section 2), in world space.
const vec2 KEY = vec2(-0.70710678, -0.70710678);

varying vec2 world;
varying flat vec2 origin;
varying flat vec2 axis_x;
varying flat vec2 axis_y;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	origin = MODEL_MATRIX[3].xy;
	// Unit axes: every length here is in world px, so the node's scale (Cosmic Collapse's) is already in
	// the uniforms and must not be applied twice.
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

float smooth01(float x) {
	x = clamp(x, 0.0, 1.0);
	return x * x * (3.0 - 2.0 * x);
}

// t of the way from a to b, as an ordered dither rather than a blend.
vec3 dither(vec3 a, vec3 b, float t, float threshold) {
	return clamp(t, 0.0, 1.0) > threshold ? b : a;
}

// The blade's half width at x px out along a blade `len` long: a narrow root in the wizard's hands that
// opens to the full width within a body's length, holds it, and comes to a needle point at the tip.
float profile(float x, float len) {
	if (x < HAND || x > len)
		return 0.0;
	float rise = min(len * 0.12, 26.0);
	float w = half_width;
	if (x < HAND + rise)
		w = mix(GRID, half_width, smooth01((x - HAND) / rise));
	float head = min(len * 0.2, half_width * 4.0 + 16.0);
	if (x > len - head)
		w = min(w, half_width * pow((len - x) / head, 0.8));
	return w;
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	vec2 rel = centre - origin;
	vec2 p = vec2(dot(rel, axis_x), dot(rel, axis_y));
	vec2 key = vec2(dot(KEY, axis_x), dot(KEY, axis_y));
	float b = bayer4(cell);
	float flicker = floor(time / 0.07);
	vec3 col = OCC;
	bool hit = false;

	// Cosmic Collapse's hole, at the tip, over the blade: a disc of void that swells to the radius its
	// burst strikes and caves in on itself, its lip lit by the key and brightening to white-hot as it
	// closes, and the dark inside it wound into arms that turn inward.
	if (hole_t >= 0.0 && hole_radius >= GRID) {
		vec2 q = p - vec2(reach, 0.0);
		float rh = length(q);
		if (rh <= hole_radius) {
			hit = true;
			float u = rh / hole_radius;
			vec2 dir = rh > 0.01 ? q / rh : vec2(0.0, -1.0);
			float lit = dot(dir, key);
			if (hole_radius - rh < GRID * 1.5) {
				col = lit > 0.35 ? (gilded ? GOLD_LIT : ARC_HOT) : (lit < -0.3 ? DARK_EDGE : DARK_MID);
				col = dither(col, gilded ? GOLD_HI : ARC_CORE, (hole_t - 0.3) * 1.6, b);
			} else if (hole_radius - rh < GRID * 2.5) {
				col = lit > 0.35 ? DARK_MID : DARK_EDGE;
			} else {
				float phi = atan(q.y, q.x);
				float arm = fract((phi + u * 4.5 - time * 7.0) * 3.0 / TAU);
				if (u > 0.3 && arm < 0.2)
					col = u > 0.68 ? DARK_MID : DARK_EDGE;
				else
					col = dither(OCC, ROBE_DEEP, (u - 0.25) * 1.8, b);
			}
		}
	}

	// The blade, ahead of the wizard and (Void Glaive) behind him.
	float len = p.x >= 0.0 ? reach : back_reach;
	float x = abs(p.x);
	float across = abs(p.y);
	float w = len > 0.0 ? profile(x, len) : 0.0;
	if (!hit && w > 0.0) {
		// The lip boils: an art pixel nibbled off it here and there, re-chosen every flicker. Only ever
		// inward, so the blade is never drawn wider than it cuts.
		float side = sign(p.y) + (p.x < 0.0 ? 4.0 : 0.0);
		float nib = hash12(vec2(floor(x / 4.0) + side * 131.0, flicker));
		float we = w - (nib > 0.74 && w > GRID * 2.0 ? GRID : 0.0);
		if (across <= we) {
			hit = true;
			float s = x / len;
			float in_from_lip = we - across;
			// The filament of arcane light down the spine, thicker in the flash of the thrust, with light
			// running down it from the hands to the tip. A seam, not a beam: the blade is void.
			float spine = max(GRID * 0.5, we * 0.11) + heat * we * 0.45;
			bool pulse = fract(x / 56.0 - time * 2.4 + seed * 0.37) < 0.16;
			if (across <= spine) {
				col = (pulse || s > 0.9 || heat > 0.5) ? ARC_CORE : ARC_HOT;
			} else if (across <= spine + GRID) {
				col = pulse || heat > 0.3 ? ARC_MID : ARC_EDGE;
			} else if (in_from_lip < GRID) {
				// The lip: lit by the key on the side that faces it, and flaring as the thrust goes out.
				float lit = dot(vec2(0.0, sign(p.y)), key);
				col = lit > 0.3 ? (gilded ? GOLD_LIT : DARK_HOT) : (lit < -0.3 ? DARK_EDGE : DARK_MID);
				if (heat > 0.2 || hash12(vec2(floor(x / 6.0), flicker + side * 7.0)) > 0.9)
					col = gilded ? GOLD_HI : ARC_MID;
			} else {
				// The void inside the blade, flowing out toward the tip: violet at the lips, falling to the
				// dark of nothing toward the filament.
				float depth = (in_from_lip - GRID) / max(GRID, we - spine - GRID * 2.0);
				float flow = 0.22 * sin(x * 0.11 - time * 13.0 + p.y * 0.2);
				col = dither(DARK_EDGE, ROBE_DEEP, depth * 1.7 + flow, b);
				col = dither(col, OCC, depth * 1.7 - 0.85 + flow, b);
				if (heat > b * 1.5)
					col = DARK_MID;
			}
		} else if (across <= hit_radius && x > HAND + 6.0) {
			// The motes the lance sheds: specks of void peeling off its lips and drifting out and along,
			// thinning toward the edge of the body's allowance and never past it.
			float k = 1.0 - (across - we) / max(GRID, hit_radius - we);
			vec2 drift = floor(vec2(x - time * 30.0, across - time * 9.0) / GRID);
			float h = hash12(drift + vec2(side * 53.0, 0.0));
			if (h > 1.0 - 0.09 * k) {
				hit = true;
				col = h > 1.0 - 0.016 * k ? ARC_MID : (k > 0.45 ? DARK_MID : DARK_EDGE);
			}
		}
	}

	// Witching Hour: the ground the hand has just swept, behind each blade as it turns - the arc it
	// covered in the last few hundredths of a second, measured on from the edge of what it cuts - densest
	// at the blade and thinning back, in streaks along it.
	if (!hit && trail > 0.0) {
		float rho = length(p);
		float behind = p.x >= 0.0 ? -atan(p.y, p.x) : atan(p.y, -p.x);
		float lenb = p.x >= 0.0 ? reach : back_reach;
		float beyond = rho * behind - hit_radius;
		float smear = rho * trail;
		if (behind > 0.0 && beyond >= 0.0 && beyond < smear && rho > HAND + 20.0 && rho < lenb - 4.0) {
			float g = 1.0 - beyond / smear;
			float streak = hash12(vec2(floor(rho / 6.0), flicker + (p.x < 0.0 ? 17.0 : 0.0)));
			if (streak > 0.4 && g * g * 0.95 > b) {
				hit = true;
				col = g > 0.66 ? DARK_MID : DARK_EDGE;
			}
		}
	}

	if (!hit || fade > b)
		discard;
	COLOR = vec4(col, 1.0);
}
