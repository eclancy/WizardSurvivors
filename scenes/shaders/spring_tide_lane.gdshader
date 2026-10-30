// Spring Tide (scripts/SpringTideLane.cs, Tituba's Apotheosis): the lane of water a Riptide wave leaves
// flooded behind it, as pixel art on the game's 2 px grid.
//
// It was a translucent blue rectangle with four rows of translucent dashes stroked along it, turned to
// the wave's heading - a wash over the floor, on a rotated grid, the smooth vector look the Bonelight
// contract rules out. Now, as in riptide_wave.gdshader, every fragment snaps itself to the WORLD's 2 px
// cell first and only then turns that cell into the lane's own frame (x along the heading from the
// caster, y across it), so the art pixels stay square and upright at any heading. Every colour is an
// opaque Water or azure tone (.ai/art-direction.md section 3), and Bayer dithering is the only gradient:
// the flood is shallow water over the ground, so the floor shows through it between the cells of water
// rather than through a translucent wash, and the current is the streaks of foam running down it.
//
// THE WATER IS THE DRAG. `front` and `tail` are SpringTideLane.FrontX and TailX and `half_width` its
// HalfWidth - the numbers the drag and the slow test an enemy's position against - so nothing is drawn
// ahead of the water's front, behind its drained tail or wider than the lane. The banks wander, but only
// INWARD of the half width (as frost_patch.gdshader's rime does), and there is no line drawn round it:
// what reads as the edge is where the water stops. The front is the foam the wave left on the ground as
// it went over; the last `drain_length` px behind the tail is the water soaking away, a Bayer dissolve,
// so the lane drains from the caster's end outward in the order the wave flooded it.
//
// One quad a lane (SpringTideLane._Draw); everything is computed per art pixel; nothing is blended.
shader_type canvas_item;
render_mode unshaded;

uniform float front = 0.0;
uniform float tail = 0.0;
uniform float half_width = 40.0;
// Tidal Wall rises as it runs, so its lane widens with it (SpringTideLane.HalfWidthAt): start_half_width at
// the caster, half_width from grow_length on. With grow_length 0 the lane is half_width all the way.
uniform float start_half_width = 40.0;
uniform float grow_length = 0.0;
// The stretch behind the tail that is still soaking away, in px. 0 when the whole lane drains at once.
uniform float drain_length = 0.0;
// 1 while the lane is whole; with no FloodSpeed the whole lane dissolves together as this falls to 0.
uniform float fade_all = 1.0;
// How fast the current runs, px a second: SpringTideLane.DragSpeed, the drag it puts on a body.
uniform float drag_speed = 150.0;
uniform float time = 0.0;
uniform float seed = 0.0;
// A legendary roll: flecks of gold in the foam, as the wave's own lip has.
uniform bool gilded = false;

const vec3 CORE = vec3(0.941, 0.980, 1.0);       // water core #F0FAFF
const vec3 HOT = vec3(0.706, 0.863, 1.0);        // water hot #B4DCFF
const vec3 MID = vec3(0.435, 0.722, 0.941);      // water mid #6FB8F0
const vec3 EDGE = vec3(0.165, 0.373, 0.769);     // water edge #2A5FC4
const vec3 AZ_SHADE = vec3(0.102, 0.243, 0.541); // azure shade #1A3E8A
const vec3 AZ_DEEP = vec3(0.055, 0.118, 0.282);  // azure deep #0E1E48
const vec3 GOLD_HI = vec3(1.0, 0.941, 0.753);    // gold hi #FFF0C0
const float GRID = 2.0;

varying vec2 v_world;
varying flat vec2 v_origin;
varying flat vec2 v_axis_x;
varying flat vec2 v_axis_y;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	v_origin = MODEL_MATRIX[3].xy;
	v_axis_x = normalize(MODEL_MATRIX[0].xy);
	v_axis_y = normalize(MODEL_MATRIX[1].xy);
}

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031 + seed * 0.0173);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

// A smooth 0..1 value noise over the lane, one lattice point every `scale` px.
float noise(vec2 p, float scale) {
	vec2 q = p / scale;
	vec2 i = floor(q);
	vec2 f = fract(q);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

vec4 shade_cell(vec2 world, vec2 origin, vec2 axis_x, vec2 axis_y) {
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	vec2 rel = centre - origin;
	vec2 p = vec2(dot(rel, axis_x), dot(rel, axis_y));
	float b = bayer4(cell);
	float x = p.x;
	float across = abs(p.y);

	if (x < tail || x > front || front <= tail)
		return vec4(0.0);

	// The lane's half width here: the wave's own width where it crossed this stretch.
	float hw = grow_length > 0.0 ? mix(start_half_width, half_width, clamp(x / grow_length, 0.0, 1.0)) : half_width;
	// The banks wander up to three art pixels in from the half width, never out past it.
	float side = p.y < 0.0 ? 17.0 : 53.0;
	float bank = hw - GRID * (0.4 + 2.6 * noise(vec2(x, side), 22.0));
	if (across > bank)
		return vec4(0.0);
	float from_bank = bank - across;

	// Draining: the water behind the tail soaks away first, dithered out toward the caster's end.
	float cover = fade_all;
	if (drain_length > 0.0 && x < tail + drain_length)
		cover = min(cover, (x - tail) / drain_length);
	if (cover <= b)
		return vec4(0.0);

	// The front: a lumpy line of foam where the wave went over last, a couple of art pixels deep.
	float lump = hash12(vec2(floor(p.y / (GRID * 2.0)), floor(time * 9.0))) ;
	float lip = GRID * (1.0 + floor(lump * 2.5));
	if (x > front - lip) {
		float h = hash12(cell + floor(time * 12.0));
		vec3 foam = h > 0.45 ? CORE : HOT;
		if (gilded && h > 0.85)
			foam = GOLD_HI;
		return vec4(foam, 1.0);
	}
	// Just behind it, the churned water the foam has run off: hot dithered over mid.
	if (x > front - lip - GRID * 3.0) {
		if (b < 0.7) {
			return vec4(b < 0.3 ? HOT : MID, 1.0);
		}
	}

	// The current: streaks of foam running down the lane at the drag's own speed, one row every few
	// art pixels across it, each broken into dashes so the eye can follow them moving.
	float rows = max(3.0, floor(hw / 9.0));
	float row_w = hw * 2.0 / rows;
	float row = floor((p.y + hw) / row_w);
	float in_row = (p.y + hw) - row * row_w;
	float period = 46.0 + 30.0 * hash12(vec2(row, 5.0));
	float dash = fract((x - time * drag_speed) / period + hash12(vec2(row, 9.0)));
	if (in_row < GRID && dash < 0.34 && from_bank > GRID) {
		return vec4(dash < 0.12 ? HOT : MID, 1.0);
	}

	// The waterline: a broken line of deep water along each bank, where the flood meets the floor.
	if (from_bank < GRID) {
		if (hash12(vec2(floor(x / GRID), side)) < 0.7) {
			return vec4(AZ_DEEP, 1.0);
		}
		return vec4(0.0);
	}

	// The body: shallow water over the ground. Deeper (denser, and darker) toward the middle of the
	// lane, thinning toward the banks, so the floor shows through between its cells - and a slow
	// swell of noise drifting with the current moves the shallows about.
	float depth = clamp(from_bank / max(GRID * 4.0, hw * 0.45), 0.0, 1.0);
	float swell = noise(vec2(x - time * drag_speed * 0.35, p.y), 30.0);
	float density = 0.26 + 0.3 * depth + 0.24 * (swell - 0.5);
	float b2 = bayer4(cell + vec2(2.0, 1.0));
	if (b2 > density)
		return vec4(0.0);
	vec3 col = swell > 0.6 && b < 0.5 ? EDGE : AZ_SHADE;
	// The odd glint of light on the surface, coming and going.
	if (hash12(cell + floor(time * 3.0) * 0.37) > 0.985)
		col = HOT;
	return vec4(col, 1.0);
}

void fragment() {
	// Godot allows no return in fragment(): the cell is shaded by shade_cell, and alpha 0 is nothing.
	vec4 c = shade_cell(v_world, v_origin, v_axis_x, v_axis_y);
	if (c.a < 0.5)
		discard;
	COLOR = c;
}
