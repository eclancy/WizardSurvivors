// Cyclone Slash's whirlwind (scripts/OrbitingBlade.cs): the running ring of wind round the wizard, as
// pixel art on the game's 2 px grid.
//
// Eric, of a level 8 screenshot: "does not look up to our typical visual fidelity." It was drawn with
// antialiased DrawArc and DrawCircle at fractional alpha: smooth see-through bands for the trails, a
// soft white disc for each blade, plain stroked arcs for the gust - UI circles, not air. Now one quad
// over the whole whirlwind carries everything and this shader draws it, as stormsweep_lash.gdshader
// and riptide_wave.gdshader draw theirs: every fragment snaps to the WORLD's 2 px cell first and is
// measured from that cell's centre, so the art pixels stay square and upright whatever the ring's
// turn; every colour is opaque and comes from the element ramp (Wind's - white-hot core, its identity
// in the mid and the edge - or the element path's, Razor Gale's Metal), with occlusion where the air
// is cut; Bayer dithering is the only gradient.
//
// THE BLADE IS THE HIT. `blade` is each outer blade's hit radius as it is now (OrbitingBlade.bladeNow,
// the CircleShape2D's radius) and `radius` the orbit it rides (radiusNow); a blade is a sickle of wind
// filling the front of exactly that circle, outlined in the ramp's edge on the circle itself and white-
// hot inside it, with a glint running off its point - never a disc, never wider than it cuts. Behind it
// a wake of cut air, dithered, its sides in the edge; then the air trails along the orbit as broken
// streaks in three lanes, cooling down the ramp (never white - the white is the blade's) and dithering
// away over `trail` radians: speed, not a band. The edge outline is what keeps a cold, white-cored
// effect readable on a pale floor (.ai/art-direction.md, "Bonelight-specific caution"). Twin Gyres' inner ring is the same drawing at its own radius and blade,
// turning the other way, a stop cooler so the outer ring still reads as the whirlwind.
//
// NO MARKER. Nothing is drawn as a ring or a disc for its own sake. The gust's eddies inside the ring
// are short broken streaks turning against the spin; Typhoon Core's are the arms of a hurricane
// winding in toward an empty eye; Vortex Reaper's drag is streaks of air spiralling in from as far as
// it reaches (PullReach) to the ring, the wind itself showing how far the drag holds.
shader_type canvas_item;
render_mode unshaded;

uniform float radius = 60.0;
uniform float blade = 10.0;
uniform int blade_count = 2;
uniform float angle = 0.0;
uniform float trail = 0.6;
uniform float gust = 0.0;
uniform float inner_radius = 0.0;
uniform float inner_blade = 10.0;
uniform int inner_count = 0;
uniform float inner_angle = 0.0;
uniform bool typhoon = false;
uniform float pull_reach = 0.0;
uniform float time = 0.0;
uniform vec4 ramp_core : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 ramp_hot : source_color = vec4(0.918, 0.988, 0.965, 1.0);
uniform vec4 ramp_mid : source_color = vec4(0.706, 0.933, 0.878, 1.0);
uniform vec4 ramp_edge : source_color = vec4(0.310, 0.682, 0.620, 1.0);

const int MAX_BLADES = 16;
const float GRID = 2.0;
const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C

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
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

// A stop of the ramp, 0 core .. 3 edge, pushed `cool` stops cooler (past the edge is the edge).
vec3 stop(float s, float cool) {
	float k = s + cool;
	return k < 0.5 ? ramp_core.rgb : (k < 1.5 ? ramp_hot.rgb : (k < 2.5 ? ramp_mid.rgb : ramp_edge.rgb));
}

// One ring of blades: n blades riding `r_ring`, each `b` its hit radius, the first at `a0`, turning
// `dir` (+1 the way the angle grows). `cool` is how many stops cooler this ring is drawn.
// Writes the colour and returns true if the cell is part of it.
bool ring(vec2 p, float pr, float pa, float r_ring, float b, int n, float a0, float dir, float tr,
		float cool, float dither, vec2 cell, inout vec3 col) {
	if (n <= 0 || r_ring <= 0.0 || abs(pr - r_ring) > b + GRID)
		return false;
	float spacing = TAU / float(n);
	for (int k = 0; k < MAX_BLADES; k++) {
		if (k >= n)
			break;
		float ak = a0 + spacing * float(k);
		vec2 out_dir = vec2(cos(ak), sin(ak));
		vec2 fwd = dir * vec2(-out_dir.y, out_dir.x);
		vec2 q = p - out_dir * r_ring;
		float x = dot(q, fwd);
		float y = dot(q, out_dir);
		float d = length(q);
		// THE BLADE: the front of its hit circle, cut by a circle set back half a radius - a sickle,
		// horns trailing. Occlusion lines the inside of the cut so it reads on a pale floor too.
		if (d <= b) {
			float back = length(vec2(x + b * 0.55, y));
			float cut = b * 0.86;
			if (back >= cut) {
				// Outlined in the ramp's edge, then white-hot: Wind is cold, and on a pale floor (the
				// Sketchbook's paper, the Frozen Waste's snow) a white rim alone is the floor's own colour.
				// The outline is the hit circle's own boundary, so it never draws past what is struck.
				float rim = b - d;
				float s = rim < GRID ? 3.0 : (rim < GRID * 2.0 ? 0.0 : (rim < GRID * 3.0 ? 1.0 : 2.0));
				// The glint: the point of the sickle flashes white as it turns, a cell or two off its tip.
				if (x > b * 0.55 && abs(y) < GRID * 1.5 && hash12(vec2(float(k), floor(time / 0.06))) > 0.4)
					s = 0.0;
				col = stop(s, cool);
				return true;
			}
			if (back >= cut - GRID) {
				col = OCC;
				return true;
			}
		}
		// THE TRAIL: the air it has just cut, behind it along the ring.
		float behind = mod(dir * (ak - pa), TAU);
		if (behind > 0.0 && behind < tr) {
			float f = behind / tr;
			float w = b * (1.0 - 0.55 * f);
			float off = pr - r_ring;
			if (abs(off) <= w) {
				float lane = off / max(w, 1.0);
				// The wake, hard behind the blade: the cut air still fills the blade's own width, opaque,
				// dithered away toward its sides and back over the first third of the trail.
				// Its sides in the edge, so the wake has a saturated boundary on any floor.
				float wake = (1.0 - f / 0.34) * (1.0 - abs(lane) * 0.75);
				if (f < 0.34 && wake > dither) {
					col = stop(abs(lane) > 0.6 ? 3.0 : (f < 0.1 ? 1.0 : (abs(lane) < 0.3 ? 2.0 : 3.0)), cool);
					return true;
				}
				int which = lane < -0.34 ? 0 : (lane > 0.34 ? 2 : 1);
				float centre = which == 1 ? 0.0 : (which == 0 ? -0.68 : 0.68) * w;
				if (abs(off - centre) <= GRID * (which == 1 ? 1.0 : 0.5)) {
					float run = behind * r_ring;
					// Then streaks: broken into dashes that ride with the blade, the outer lanes shorter and
					// sparser, the middle one unbroken while it is close.
					float dash = hash12(vec2(float(which) + float(k) * 3.0, floor(run / (which == 1 ? 9.0 : 6.0))));
					float keep = which == 1 ? (f < 0.5 ? -1.0 : 0.25) : 0.45;
					if (dash > keep && (1.0 - f) * (which == 1 ? 1.2 : 0.9) > dither) {
						// Never the white: that is the blade's alone, and a white streak is lost on a pale floor.
						float s = f < 0.3 ? 1.0 : (f < 0.65 ? 2.0 : 3.0);
						col = stop(s + (which == 1 ? 0.0 : 1.0), cool);
						return true;
					}
				}
			}
		}
	}
	return false;
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	vec2 p = inverse(mat2(axis_x, axis_y)) * (centre - origin);
	float pr = length(p);
	float pa = atan(p.y, p.x);
	float dither = bayer4(cell);
	vec3 col = vec3(0.0);
	bool hit = false;

	// The outer ring, then Twin Gyres' inner one (the other way, a stop cooler).
	hit = ring(p, pr, pa, radius, blade, blade_count, angle, 1.0, trail, 0.0, dither, cell, col);
	if (!hit)
		hit = ring(p, pr, pa, inner_radius, inner_blade, inner_count, inner_angle, -1.0, 0.9, 1.0, dither, cell, col);

	// The gust's eddies: short broken streaks inside the ring, turning against the spin - more and
	// longer the harder he runs. One art pixel thick; never a circle, never a fill.
	if (!hit && pr < radius - blade * 0.6 && pr > radius * 0.55) {
		int eddies = 3 + int(round(5.0 * gust));
		float len = mix(0.35, 0.8, gust);
		for (int s = 0; s < 8; s++) {
			if (s >= eddies)
				break;
			float start = -angle * 0.7 + float(s) * TAU / float(eddies);
			float er = radius * (s % 2 == 0 ? 0.8 : 0.92);
			if (abs(pr - er) > GRID * 0.5)
				continue;
			float along = mod(pa - start, TAU);
			if (along < len) {
				float f = along / len;
				float dash = hash12(vec2(float(s), floor(along * er / 7.0)));
				if (dash > 0.35 && (1.0 - f) * (0.55 + 0.45 * gust) > dither) {
					col = f < 0.3 ? ramp_hot.rgb : (f < 0.65 ? ramp_mid.rgb : ramp_edge.rgb);
					hit = true;
					break;
				}
			}
		}
	}

	// Typhoon Core: the hurricane's arms winding in from the ring toward an empty eye on the wizard,
	// turning with the storm. Thin, broken, cooling as they wind in.
	if (!hit && typhoon && pr > radius * 0.3 && pr < radius - blade * 0.4) {
		float t = pr / radius;
		float arm = fract((pa * 3.0 + log(t) * 5.5 - angle * 1.2) / TAU);
		float band = min(arm, 1.0 - arm) * TAU * pr / 3.0;
		if (band < GRID * 0.75) {
			float dash = hash12(vec2(floor(pr / 6.0), floor(pa * 3.0 / TAU + 0.5)));
			if (dash > 0.3 && t * 1.1 > dither) {
				col = t > 0.75 ? ramp_hot.rgb : (t > 0.5 ? ramp_mid.rgb : ramp_edge.rgb);
				hit = true;
			}
		}
	}

	// Vortex Reaper: streaks of air dragged in from as far as the drag reaches to the ring, moving in,
	// densest at the ring and thinning out to the reach.
	if (!hit && pull_reach > 0.0 && pr > radius + blade && pr < pull_reach) {
		float t = (pr - radius - blade) / max(1.0, pull_reach - radius - blade);
		float arm = fract((pa * 6.0 - (pr / radius) * 2.2 - angle * 0.5) / TAU);
		float band = min(arm, 1.0 - arm) * TAU * pr / 6.0;
		if (band < GRID * 0.75) {
			float dash = hash12(vec2(floor((pr + time * 110.0) / 10.0), floor(pa * 6.0 / TAU + 0.5)));
			if (dash > 0.45 && (1.0 - t) * 0.9 > dither) {
				col = t < 0.25 ? ramp_mid.rgb : ramp_edge.rgb;
				hit = true;
			}
		}
	}

	if (!hit)
		discard;
	// The node's modulate (a Legendary's gold) rides in on COLOR.
	COLOR = vec4(col, 1.0) * COLOR;
}
