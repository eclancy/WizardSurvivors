// Molten Shard (scripts/MoltenShardLook.cs, on scripts/ElementalBolt.cs): a splinter of white-hot metal,
// tumbling, shedding sparks - as pixel art on the game's 2 px grid.
//
// It was a 32x32 sprite of a bar, turned to the bolt's heading, multiplied by a tint and trailed by
// untextured particles: at any heading but the four square ones its art pixels sat on a rotated grid,
// the tint pushed the Metal ramp off the palette, and the particles were translucent specks smaller
// than an art pixel. Now, as in riptide_wave.gdshader and void_lance.gdshader, every fragment snaps
// itself to the WORLD's 2 px cell first and only then turns that cell into the shard's frame (x along
// it, y across) to decide its colour, so the art pixels stay square and upright however it tumbles.
// Every colour is opaque: the Metal ramp is the shard - its rim lit by the key from the upper left
// (.ai/art-direction.md section 2) on one side, its edge stop on the other - and the Fire ramp is the
// heat in it: a seam of molten metal down its spine, a white-hot point, a torn end glowing where it
// broke off, and the sparks it sheds. Bayer dithering is the only gradient; nothing is blended.
//
// THE SHARD IS THE HIT. `hit_radius` is ElementalBolt.HitRadius, the collision circle's radius in the
// node's own units, so an evolution's scale grows the shard and the circle together, and `body_allowance` is a
// body's allowance (RiptideWave.BodyAllowance, in node units): the shard's half-length is the two, the
// reach from its centre at which an enemy's centre is struck - the way the Riptide wave and the Gale
// Blade are drawn - so as it tumbles its points trace what it hits. It is the length the old sprite
// was on screen. The streak and the sparks are thrown BEHIND it along its flight, never ahead of it.
//
// THREE STATES (`mode`):
//   0  in flight: tumbling end over end, shedding sparks behind.
//   1  lodged (Magma Core): still in what it struck, heating through the Fire ramp as `heat` runs to 1,
//      spitting sparks round itself - the tell that it is about to erupt.
//   2  forging (Forge Swarm): held in the ring round the wizard, point out, glowing and still.
// The node's modulate (an evolution's tint, a Legendary's gold) is overridden: a multiply over these
// ramps only muddied them. A Legendary's gold is `gilded`, in the seam and the point.
shader_type canvas_item;
render_mode unshaded;

uniform float hit_radius = 8.0;
uniform float body_allowance = 12.0;
uniform float seed = 0.0;
uniform int mode = 0;
// 0 a shard in flight, rising to 1 the moment before a lodged core erupts.
uniform float heat = 0.0;
uniform bool gilded = false;

const vec3 FIRE_CORE = vec3(1.0, 0.957, 0.816);      // fire core #FFF4D0
const vec3 FIRE_HOT = vec3(1.0, 0.784, 0.282);       // fire hot #FFC848
const vec3 FIRE_MID = vec3(0.910, 0.478, 0.102);     // fire mid #E87A1A
const vec3 FIRE_EDGE = vec3(0.722, 0.220, 0.102);    // fire edge #B8381A
const vec3 METAL_HOT = vec3(0.918, 0.949, 0.980);    // metal hot #EAF2FA
const vec3 METAL_MID = vec3(0.769, 0.831, 0.910);    // metal mid #C4D4E8
const vec3 METAL_EDGE = vec3(0.369, 0.447, 0.565);   // metal edge #5E7290
const vec3 GOLD_HI = vec3(1.0, 0.941, 0.753);        // gold hi #FFF0C0
const vec3 GOLD_LIT = vec3(0.847, 0.690, 0.290);     // gold lit #D8B04A
const float GRID = 2.0;
// The key light, from the upper left, in world space.
const vec2 KEY = vec2(-0.70710678, -0.70710678);

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
	vec3 p3 = fract(vec3(p.xyx) * 0.1031 + seed * 0.0137);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

mat2 rot(float a) {
	float c = cos(a);
	float s = sin(a);
	return mat2(vec2(c, s), vec2(-s, c));
}

// Half the width, in world px, of a line one art pixel thick whose normal is `n` (world, unit): a
// cell belongs to the line when the line crosses its square, so a thin line stays unbroken at any
// angle - one cell a row when it is square to the grid, a staircase when it is not.
float pixel_half(vec2 n) {
	return 0.5 * GRID * (abs(n.x) + abs(n.y));
}

// A spark's colour as it cools, 0 just thrown .. 1 gone out.
vec3 cooling(float c) {
	if (c < 0.2) return FIRE_CORE;
	if (c < 0.45) return FIRE_HOT;
	if (c < 0.7) return FIRE_MID;
	return FIRE_EDGE;
}

void fragment() {
	// The world's art pixel, then where its centre sits in the bolt's frame (x along the heading).
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	mat2 m = mat2(axis_x, axis_y);
	mat2 to_node = inverse(m);
	vec2 p = to_node * (centre - origin);
	// Node units per world px: the shard is drawn in node units, so an evolution's scale grows it, but
	// a rim or a spark is one art pixel at any scale.
	float px = 1.0 / max(length(axis_x), 0.001);
	float art = GRID * px;
	float b = bayer4(cell);

	float half_len = hit_radius + body_allowance;
	float half_w = max(half_len * 0.2, art * 1.5);
	// Where the shard is widest: a little behind its middle, so it reads as a point leading a torn end.
	float waist = -0.2 * half_len;

	// Tumbling in flight; held point-out in the ring and in a body.
	float spin = mode == 0 ? TIME * 10.0 + seed * 3.1 : 0.0;
	vec2 q = rot(-spin) * p;
	float w = q.x > waist
		? half_w * (half_len - q.x) / (half_len - waist)
		: half_w * (q.x + half_len) / (waist + half_len);
	float across = abs(q.y);
	// The shard's axis and its normal, in world space, for one-pixel lines along it.
	vec2 normal_w = normalize(m * (rot(spin) * vec2(0.0, 1.0)));
	float thin = pixel_half(normal_w) * px;

	vec3 col = FIRE_EDGE;
	bool hit = false;
	if (abs(q.x) <= half_len && across <= w && w > 0.0) {
		hit = true;
		// Which side of the shard faces the key light.
		float lit = dot(normal_w * sign(q.y + 0.0001), KEY);
		float from_rim = w - across;
		bool rim = from_rim < art;
		bool spine = across <= thin;
		float t = (q.x + half_len) / (2.0 * half_len); // 0 torn end .. 1 point

		// The metal, as heat takes it: a shard in flight is steel with a molten seam; a lodged core
		// runs through the Fire ramp toward white as its fuse burns down, and a forged one glows.
		vec3 body = METAL_MID;
		vec3 rim_lit = METAL_HOT;
		vec3 rim_dark = METAL_EDGE;
		vec3 seam = FIRE_HOT;
		float h = clamp(heat + (mode == 2 ? 0.3 : 0.0), 0.0, 1.0);
		if (h > 0.0) {
			body = h > 0.66 ? FIRE_HOT : (h > 0.33 ? FIRE_MID : (b < h * 3.0 ? FIRE_MID : METAL_MID));
			rim_dark = h > 0.5 ? FIRE_MID : FIRE_EDGE;
			rim_lit = h > 0.5 ? FIRE_CORE : METAL_HOT;
			seam = h > 0.5 ? FIRE_CORE : FIRE_HOT;
			// Near the end of the fuse it throbs: the whole shard flashes white-hot and back.
			if (mode == 1 && h > 0.7 && fract(TIME * (4.0 + 14.0 * h)) < 0.35)
				body = FIRE_CORE;
		}
		if (gilded)
			seam = GOLD_HI;

		col = body;
		if (rim)
			col = lit > 0.0 ? rim_lit : rim_dark;
		if (spine && from_rim >= art * 0.5)
			col = seam;
		// The point, white-hot; the torn end, still glowing where it broke.
		if (t > 0.86)
			col = gilded ? GOLD_HI : FIRE_CORE;
		else if (t < 0.18)
			col = (t < 0.08 || b < 0.5) ? FIRE_EDGE : FIRE_MID;
		if (gilded && rim && lit > 0.0 && t > 0.5)
			col = GOLD_LIT;
	}

	// The heat round it: an art pixel of the Fire ramp's edge hugging the shard - a glow on the dark
	// floors and the contour that keeps pale steel from vanishing on the pale ones (sand, snow, paper).
	if (!hit && abs(q.x) <= half_len + art && w > -art && across <= max(w, 0.0) + art) {
		col = (mode == 1 && heat > 0.5) ? FIRE_MID : FIRE_EDGE;
		hit = true;
	}

	if (!hit && mode == 0) {
		// Behind it along the flight: a streak of glowing slag a pixel wide, cooling as it trails off.
		float x = p.x;
		float from_x = -half_len * 0.4;
		float streak_len = half_len * 1.3;
		float streak_thin = pixel_half(normalize(axis_y)) * px;
		if (x < from_x && x > from_x - streak_len && abs(p.y) <= streak_thin) {
			float u = (from_x - x) / streak_len;
			if (u < 0.35 || b > u) {
				col = u < 0.3 ? FIRE_HOT : (u < 0.6 ? FIRE_MID : FIRE_EDGE);
				hit = true;
			}
		}
		// And the sparks it sheds, each falling back and stepping down the ramp until it goes out. A
		// spark is one art pixel: its position is snapped to the world's grid and compared as a cell.
		for (int k = 0; k < 6 && !hit; k++) {
			float fk = float(k);
			float c = fract(TIME * 2.3 + fk / 6.0 + seed * 0.41);
			if (c > 0.92)
				continue;
			float side = hash12(vec2(fk, 3.0)) * 2.0 - 1.0;
			vec2 at = vec2(-half_len * 0.5 - c * half_len * 1.9, side * half_w * (0.5 + 1.6 * c));
			vec2 at_cell = floor((origin + m * at) / GRID);
			if (at_cell == cell) {
				col = cooling(c);
				hit = true;
			}
		}
	}

	if (!hit && mode == 1) {
		// Lodged: sparks spat out of the wound all round it, more and faster as the fuse burns down -
		// all inside the shard's own circle and a pixel, nothing that reads as the eruption's reach.
		int sparks = 3 + int(heat * 5.0);
		float rate = 1.8 + 2.5 * heat;
		for (int k = 0; k < 8 && !hit; k++) {
			if (k >= sparks)
				break;
			float fk = float(k);
			float run = TIME * rate + fk / float(sparks) + seed * 0.3;
			float c = fract(run);
			float a = hash12(vec2(fk, floor(run))) * 6.2831853;
			vec2 at = vec2(cos(a), sin(a)) * (half_w + c * (half_len - half_w));
			vec2 at_cell = floor((origin + m * at) / GRID);
			if (at_cell == cell) {
				col = cooling(c);
				hit = true;
			}
		}
	}

	if (!hit)
		discard;
	COLOR = vec4(col, 1.0);
}
