// Solar Flare (scripts/SolarFlareVisual.cs, fired by ElementalPulse): the burst of light that goes off
// round the wizard on every pulse, Corona Discharge's sunbeams with it, and - on a second material - the
// pool of light gathering at his boots while he stands still. As pixel art on the game's 2 px grid.
//
// It was drawn with DrawArc, DrawCircle and translucent polygons: two smooth hoops at 95% and 60%
// alpha, spokes and beams as see-through wedges, an antialiased line down each beam, and the charge as
// two soft discs squashed onto the floor. Every one of those is a thing the Bonelight contract rules
// out (.ai/art-direction.md section 3), and the hoops lingered on the radius for the back half of
// the burst - a ring held on the reach, which in a still frame is an area marker. Here every fragment
// snaps to the WORLD's 2 px cell first and is measured from the snapped centre, so the art pixels are
// square and upright, and every colour is an opaque Light or Fire tone; Bayer dithering is the only
// gradient, and the burst thins and goes out by dither rather than by alpha.
//
// THE RADIUS IS THE HIT. `radius` is the reach ElementalPulse.TriggerPulse tested this pulse - grown by
// however long he stood still - in WORLD px, whatever the node's scale. Nothing of the burst is drawn
// past it: the rays stop at 0.95 of it, the embers inside it, and the shock front - the one-shot
// shockwave of the flare's own light that section 3 allows - lands on it at the end of the burst and
// breaks up there, so it is never a line held on the radius. Corona Discharge's beams are drawn at the
// length and the half width they hit at (ElementalPulse.FireCoronaBeams), thinning only afterwards.
//
// THE POOL is not an area: it says "building", and the burst says how far. It sits at his boots on the
// ground layer (DrawLayers.GroundTell, under every body), inside the Brazier's ember ring as before.
shader_type canvas_item;
render_mode unshaded;

// false: the burst. true: the charge pool at his boots.
uniform bool pool = false;
// The burst: world px reach, 0..1 through it, and the turn of its ray pattern.
uniform float radius = 90.0;
uniform float progress = 0.0;
uniform float spin = 0.0;
uniform int rays = 14;
uniform int embers = 12;
// Corona Discharge's sunbeams: how many, how long and how wide they HIT (world px), and their turn.
uniform int beam_count = 0;
uniform float beam_length = 0.0;
uniform float beam_half = 14.0;
uniform float beam_turn = 0.0;
// The pool: how far the flare has built (0..1), a running clock, and where his boots are (world px
// from the node's origin).
uniform float charge = 0.0;
uniform float clock = 0.0;
uniform vec2 boots = vec2(0.0, 36.0);

const vec3 LIGHT_CORE = vec3(1.0, 1.0, 1.0);          // light core #FFFFFF
const vec3 LIGHT_HOT = vec3(1.0, 0.973, 0.847);       // light hot #FFF8D8
const vec3 LIGHT_MID = vec3(1.0, 0.941, 0.659);       // light mid #FFF0A8
const vec3 LIGHT_EDGE = vec3(1.0, 0.776, 0.235);      // light edge #FFC63C
const vec3 FIRE_HOT = vec3(1.0, 0.784, 0.282);        // fire hot #FFC848
const vec3 FIRE_MID = vec3(0.910, 0.478, 0.102);      // fire mid #E87A1A
const vec3 FIRE_EDGE = vec3(0.722, 0.220, 0.102);     // fire edge #B8381A

const float GRID = 2.0;
// The pool lies on the floor, seen from the game's three-quarter view.
const float POOL_SQUASH = 0.36;
const int MAX_EMBERS = 16;

varying vec2 world;
varying flat vec2 origin;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	origin = MODEL_MATRIX[3].xy;
}

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

float hash11(float n) {
	return fract(sin(n * 12.9898 + 4.1414) * 43758.5453);
}

float wrap_angle(float x) {
	return x - TAU * floor((x + PI) / TAU);
}

// The burst at a cell p (world px from the snapped centre). Later parts are drawn over earlier ones:
// beams, rays, the trailing wave, the front, embers, the kernel.
bool burst(vec2 cell, vec2 p, float b, out vec3 col) {
	col = LIGHT_CORE;
	bool hit = false;
	float d = length(p);
	float a = atan(p.y, p.x);
	float wave = 1.0 - (1.0 - progress) * (1.0 - progress);
	float fade = 1.0 - progress;
	float r = radius;

	// Corona Discharge: each beam shoots out to its full length in the first third of the burst, at the
	// width it hits, then thins and goes out by dither. White-hot down the middle, the flare's gold and
	// orange to its edges, and a knot of light running out along it.
	if (beam_count > 0 && beam_length > 0.0 && d > r * 0.15) {
		float step_a = TAU / float(beam_count);
		float k = floor(wrap_angle(a - beam_turn) / step_a + 0.5);
		vec2 dir = vec2(cos(beam_turn + k * step_a), sin(beam_turn + k * step_a));
		float along = dot(p, dir);
		float across = abs(p.x * dir.y - p.y * dir.x);
		float reach = beam_length * clamp(wave * 3.0, 0.0, 1.0);
		float half_w = max(GRID, beam_half * mix(0.35, 1.0, fade * fade));
		if (along > 0.0 && along < reach && across < half_w) {
			float s = across / half_w;
			float tip = along / max(GRID, beam_length);
			float knot = fract(along / 46.0 - progress * 5.0);
			float lvl = s + (tip > 0.88 ? 0.35 : 0.0) - (knot < 0.18 ? 0.25 : 0.0);
			col = lvl < 0.22 ? LIGHT_CORE : (lvl < 0.5 ? LIGHT_HOT : (lvl < 0.78 ? LIGHT_EDGE : FIRE_MID));
			// The outer band breaks up first; the whole beam dithers out over the back half.
			bool keep = s < 0.78 || b < 0.55;
			hit = keep && b < fade * 1.9;
		}
	}

	if (d > r - GRID * 0.5)
		return hit;

	// The rays: tapering spokes of light from the kernel, long and short by turns, growing with the
	// wave and thinning as it fades - never past 0.95 of the reach.
	int n = max(4, rays);
	float step_r = TAU / float(n);
	float rk = floor(wrap_angle(a - spin) / step_r + 0.5);
	float off = wrap_angle(a - spin - rk * step_r);
	float len_scale = mod(rk + 1024.0, 2.0) < 0.5 ? 1.0 : 0.62;
	float inner = r * 0.1;
	float outer = inner + (r * 0.95 - inner) * wave * len_scale;
	if (d > inner && d < outer) {
		float t = (d - inner) / max(GRID, outer - inner);
		float w = max(GRID * 0.5, r * 0.055 * len_scale * (0.4 + 0.6 * fade) * (1.0 - t));
		if (abs(off) * d < w && b < fade * 1.6 + 0.1) {
			col = t < 0.3 ? LIGHT_HOT : (t < 0.62 ? LIGHT_MID : (t < 0.86 ? LIGHT_EDGE : FIRE_MID));
			hit = true;
		}
	}

	// The shock front: a band of light leaving the kernel fast and coasting to the reach, white-hot when
	// it leaves and cooling through gold to the flare's orange as it goes. It thins as it travels and
	// breaks up into dither as it arrives, so it lands on the radius and is gone.
	float lead = r * wave;
	float thick = GRID * mix(1.0, 4.0, fade);
	if (d <= lead && d > lead - thick && b < fade * 1.7) {
		bool leading = d > lead - GRID;
		if (wave < 0.4)
			col = leading ? LIGHT_CORE : LIGHT_HOT;
		else if (wave < 0.75)
			col = leading ? LIGHT_MID : LIGHT_EDGE;
		else
			col = leading ? FIRE_HOT : FIRE_MID;
		hit = true;
	}
	// A dimmer wave behind it: the flare's burnt edge, scattered.
	float trail = lead * 0.68;
	if (d <= trail && d > trail - GRID * 2.0 && b < fade * 0.8) {
		col = wave < 0.5 ? FIRE_HOT : FIRE_EDGE;
		hit = true;
	}

	// Embers riding the wave out, one or two art pixels each, cooling down the fire ramp. Laid out from
	// their index, so nothing is stored per pulse; inside the reach by their own size.
	int m = min(embers, MAX_EMBERS);
	for (int i = 0; i < MAX_EMBERS; i++) {
		if (i >= m)
			break;
		float fi = float(i);
		float ea = spin * 0.6 + TAU * fract(fi * 0.6180339);
		float er = r * (0.55 + 0.4 * fract(fi * 0.4142 + 0.2));
		float size = mod(fi, 3.0) < 1.0 ? GRID * 2.0 : GRID;
		vec2 at = vec2(cos(ea), sin(ea)) * min(er * wave, r - size);
		vec2 c0 = floor((at - size * 0.5) / GRID + 0.5) * GRID;
		if (p.x >= c0.x && p.x < c0.x + size && p.y >= c0.y && p.y < c0.y + size && progress < 0.9) {
			col = progress < 0.2 ? LIGHT_CORE : (progress < 0.42 ? FIRE_HOT : (progress < 0.66 ? FIRE_MID : FIRE_EDGE));
			hit = true;
		}
	}

	// The kernel: white-hot at his middle for the first moment, small on purpose - it is the only
	// near-white mass, and at full reach it would be the opaque blob this replaced.
	float core_fade = clamp(1.0 - progress * 2.6, 0.0, 1.0);
	float cr = r * 0.2 * core_fade;
	if (cr > GRID * 0.5) {
		if (d < cr * 0.55) {
			col = LIGHT_CORE;
			hit = true;
		} else if (d < cr) {
			col = b < 0.5 ? LIGHT_CORE : LIGHT_HOT;
			hit = true;
		} else if (d < cr * 1.9 && b < 0.7 * core_fade) {
			col = d < cr * 1.4 ? LIGHT_MID : FIRE_HOT;
			hit = true;
		}
	}
	return hit;
}

// The pool of light at his boots: a dithered patch that widens and fills as the charge climbs and
// throbs quicker as it fills, with motes drawn in to it across the ground - the sun gathering itself.
bool gathering(vec2 cell, vec2 p, float b, out vec3 col) {
	col = LIGHT_MID;
	vec2 q = vec2(p.x, p.y / POOL_SQUASH);
	float d = length(q);
	float throb = 0.5 + 0.5 * sin(clock * mix(3.0, 7.0, charge));
	float pr = mix(10.0, 26.0, charge) * mix(0.92, 1.08, throb);
	bool hit = false;
	if (d < pr) {
		float u = d / pr;
		// Denser and whiter toward the middle, and more of it as the charge climbs.
		float density = (0.25 + 0.65 * charge) * (1.15 - 0.6 * u);
		if (b < density) {
			col = u < 0.35 && charge > 0.5 ? LIGHT_CORE : (u < 0.7 ? LIGHT_HOT : LIGHT_MID);
			hit = true;
		}
	} else if (d < pr * 1.35 && b < 0.1 + 0.25 * charge) {
		col = LIGHT_EDGE;
		hit = true;
	}
	int motes = 3 + int(round(6.0 * charge));
	for (int i = 0; i < 9; i++) {
		if (i >= motes)
			break;
		float fi = float(i);
		float cycle = fract(clock * 0.9 + fi * 0.6180339);
		float ma = spin + TAU * fract(fi * 0.381966) + clock * 0.4;
		vec2 at = vec2(cos(ma), sin(ma) * POOL_SQUASH) * mix(pr * 2.4, pr * 0.7, cycle);
		vec2 c0 = floor((at - GRID * 0.5) / GRID + 0.5) * GRID;
		if (p.x >= c0.x && p.x < c0.x + GRID && p.y >= c0.y && p.y < c0.y + GRID && sin(cycle * PI) > 0.2) {
			col = cycle > 0.6 ? LIGHT_CORE : LIGHT_EDGE;
			hit = true;
		}
	}
	return hit;
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	// The centre on a grid line of the world, so the art is symmetrical about it and never straddles
	// a cell as the wizard walks.
	vec2 at = pool ? origin + boots : origin;
	vec2 c0 = floor(at / GRID + 0.5) * GRID;
	vec2 p = centre - c0;
	float b = bayer4(cell);
	vec3 col = LIGHT_MID;
	bool hit = false;
	if (pool)
		hit = gathering(cell, p, b, col);
	else
		hit = burst(cell, p, b, col);
	if (!hit)
		discard;
	// The node's modulate - a Legendary's gold, an evolution's warmth (Player.ApplyLegendaryVisual) -
	// multiplied back in, as it always was over this spell.
	COLOR = vec4(col, 1.0) * COLOR;
}
