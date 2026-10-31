// Hollow Star's well (scripts/HollowStar.cs): the black core, its lip, the three arms that spiral in
// from the edge of the pull, Eclipse's corona and Black Sun's prominences, as pixel art on the game's
// 2 px grid.
//
// They were DrawCircle, DrawArc and DrawPolyline in translucent violets: a soft disc with a smooth ring
// round it and three anti-aliased curves, the vector look the Bonelight contract rules out
// (.ai/art-direction.md sections 1 and 3). Eric, 2026-10-29: "does not look up to our typical visual
// fidelity". Here, as in tentacle_rift.gdshader and gravewell_collapse.gdshader, every fragment snaps to
// the WORLD's 2 px cell first and that cell is measured once against the well, so every part is square,
// upright art pixels that stay put on the floor while the well turns and, under Twin Stars, drifts.
// Every colour is opaque and a Bonelight tone: the element ramp the well burns in (Darkness, or Light on
// Eclipse) for the arms, the lip, the prominences and the corona, and occlusion and the violet row's
// deep for the hole. Ordered dither is the only gradient; nothing is blended and nothing antialiased.
//
// THE ARMS ARE THE REACH (art-direction section 3, "no area markers"). They are laid out exactly as the
// old polylines were - three of them, each turning 3.2 radians from `arm_outer` to `arm_inner` - and
// `arm_outer` grows to `pull_radius`, HollowStar.PullRadius, the reach its pull tests: the tip of every
// arm stops on it, and nothing here is drawn further out. The core is `core`, the radius the node
// swells as the pull runs (and under Crush, with the knot). No ring marks anything.
//
// THE HOLE IS A HOLE. The near wall (upper left, toward the key) is in the dark; the far wall faces the
// key and catches it, so the lip on the lower right is the ramp's hot and its inner wall the ramp's edge
// stepping down into occlusion - a bowl, as crater.gdshader lights one. On the Spirit Mirror (John Dee's
// Apotheosis) the hole is Dee's obsidian scrying glass instead: black glass with one hard reflection
// across it, the specular as a line (obsidian_glass.py's rule), so the form is told from the spell.
//
// THE HEAT IS THE FALLOFF (0.9.3, Eric: "enemies should take more damage the closer they are to the
// center"). The collapse hits hardest at the centre and least at its edge (HollowStar.ShareAt), so the
// floor of the well between the lip and `heat_radius` (the collapse radius) glows: cells of the ramp,
// opaque, by ordered dither - densest and hottest against the core, thinning and cooling to nothing at the
// edge of the collapse, in slow turning bands like an accretion disc. `heat` (0..1) is how bright it
// burns: a smoulder as the well opens, building through the pull, a flare as the core shuts - and with the
// hole gone, the flare is a disc hottest at its centre. No ring: it fades out, it is never outlined.
//
// The node's modulate (a Legendary's gold) arrives in COLOR and is multiplied in.
shader_type canvas_item;
render_mode unshaded;

uniform float pull_radius = 150.0;
uniform float arm_inner = 24.0;
uniform float arm_outer = 150.0;
uniform float core = 24.0;
uniform float spin = 0.0;
uniform float clock = 0.0;
// Eclipse: the corona hugging the core, 0..1 (0: none).
uniform float corona = 0.0;
uniform bool black_sun = false;
uniform bool mirror = false;
// The collapse's falloff: its radius, and how bright it burns (0: not drawn).
uniform float heat_radius = 80.0;
uniform float heat = 0.0;
uniform vec4 ramp_core : source_color = vec4(0.910, 0.847, 1.0, 1.0);
uniform vec4 ramp_hot : source_color = vec4(0.706, 0.549, 0.910, 1.0);
uniform vec4 ramp_mid : source_color = vec4(0.541, 0.361, 0.769, 1.0);
uniform vec4 ramp_edge : source_color = vec4(0.208, 0.125, 0.361, 1.0);

const float GRID = 2.0;
const vec2 KEY = vec2(-0.70710678, -0.70710678);
const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C
const vec3 VIOLET_DEEP = vec3(0.078, 0.063, 0.122);  // violet deep #14101F
const vec3 STEEL_LIT = vec3(0.722, 0.784, 0.863);    // steel lit #B8C8DC
const vec3 STEEL_HI = vec3(0.863, 0.910, 0.957);     // steel hi #DCE8F4

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

float wrap_angle(float a) {
	return a - TAU * floor((a + PI) / TAU);
}

void fragment() {
	vec2 cell = floor(world / GRID);
	vec2 p = (cell + 0.5) * GRID - origin;
	float r = length(p);
	float phi = atan(p.y, p.x);
	vec2 dir = r > 0.01 ? p / r : vec2(0.0, 1.0);
	float facing = dot(dir, KEY);
	float b = bayer4(cell);
	vec3 col = vec3(0.0);
	bool hit = false;

	// The hole. Its lip is one art pixel; the far (lower-right) wall below it is lit by the key, the
	// near wall is dark, and the bottom is occlusion.
	if (core > 1.0 && r <= core) {
		hit = true;
		float depth = (core - r) / core;
		if (core - r < GRID) {
			col = facing < -0.2 ? ramp_hot.rgb : ramp_mid.rgb;
		} else if (mirror) {
			// Black glass: a hard reflection across it, steel hi on its crest, and the rest of the disc
			// violet deep fading by dither into occlusion toward the near edge.
			float band = dot(p, vec2(0.70710678, -0.70710678)) + core * 0.22;
			if (abs(band) < GRID * 0.75 && depth > 0.15)
				col = STEEL_HI;
			else if (abs(band) < GRID * 1.75 && depth > 0.1)
				col = STEEL_LIT;
			else
				col = b < 0.35 + 0.4 * (-facing) ? VIOLET_DEEP : OCC;
		} else if (facing < -0.15 && core - r < GRID * 3.0) {
			col = core - r < GRID * 2.0 ? ramp_edge.rgb : (b < 0.5 ? VIOLET_DEEP : ramp_edge.rgb);
		} else if (depth < 0.35 && b < (0.35 - depth) * 2.0) {
			col = VIOLET_DEEP;
		} else {
			col = OCC;
		}
	}

	// Black Sun: seven prominences licking off the lip, turning slowly - tongues of the ramp, a white
	// point at the tip of each.
	if (!hit && black_sun && core > 1.0 && r > core) {
		for (int i = 0; i < 7; i++) {
			float fi = float(i);
			float a = fi * TAU / 7.0 + spin * 0.25;
			float lick = 0.25 + 0.2 * sin(spin * 1.7 + fi * 2.3);
			float reach = core * (1.0 + lick);
			if (r > reach + GRID)
				continue;
			float s = clamp((r - core * 0.92) / max(reach - core * 0.92, 1.0), 0.0, 1.0);
			float across = wrap_angle(phi - a) * r - core * 0.12 * sin(spin + fi) * s;
			float w = mix(core * 0.14, GRID * 0.5, s);
			if (abs(across) < w) {
				hit = true;
				col = s > 0.88 ? ramp_core.rgb : (abs(across) < GRID * 0.75 ? ramp_hot.rgb : ramp_mid.rgb);
				break;
			}
		}
	}

	// Eclipse's corona: the light round a black disc, densest at the lip and thinning out, its outer edge
	// ragged and flickering. Inside the collapse radius: the well's own rim, not a mark of its reach.
	if (!hit && corona > 0.0 && core > 1.0 && r > core) {
		float sector = floor((phi / TAU + 0.5) * 16.0);
		float ragged = fract(sin(sector * 12.9898 + floor(clock * 12.0) * 3.7) * 43758.5453);
		float band = core * (0.18 + 0.12 * ragged) * corona + GRID;
		float u = (r - core) / band;
		if (u < 1.0 && b > u * 0.95) {
			hit = true;
			col = u < 0.35 ? ramp_core.rgb : ramp_hot.rgb;
		}
	}

	// The arms: three, spiralling in from the reach to the core, thin at the tip and thicker inward - a
	// one-pixel hot spine in a body of the ramp's mid, with a dithered fringe of its edge.
	if (!hit && arm_outer > arm_inner && r >= arm_inner && r <= arm_outer) {
		float t = (arm_outer - r) / (arm_outer - arm_inner);
		// Never under one and a quarter art pixels, or a thin arm on a long curve breaks into dashes.
		float w = mix(GRID * 1.25, GRID * 2.5, t);
		for (int k = 0; k < 3; k++) {
			float theta = spin + float(k) * TAU / 3.0 + t * 3.2;
			float across = abs(wrap_angle(phi - theta)) * r;
			if (across < GRID * 0.75) {
				hit = true;
				col = ramp_hot.rgb;
				break;
			} else if (across < w) {
				hit = true;
				col = ramp_mid.rgb;
				break;
			} else if (across < w + GRID * 1.5 && b < 0.5 - 0.3 * t) {
				hit = true;
				col = ramp_edge.rgb;
				break;
			}
		}
	}

	// The heat, under everything above: the arms and the corona cross it. 0 at the lip (or the centre,
	// once the core has shut), 1 at the collapse's edge; density falls as the square of the way out, so
	// the core reads solid and the edge sparse, and the colour steps down the ramp with it.
	if (!hit && heat > 0.0 && r < heat_radius) {
		float lip = max(core, 0.0);
		float u = clamp((r - lip) / max(heat_radius - lip, 1.0), 0.0, 1.0);
		if (r >= lip) {
			float swirl = 0.5 + 0.5 * sin(phi * 3.0 - spin * 0.8 + r * 0.09);
			float inward = 1.0 - u;
			float density = heat * inward * inward * (0.6 + 0.4 * swirl);
			if (b < density) {
				hit = true;
				if (u < 0.15 && heat > 0.9)
					col = ramp_core.rgb;
				else if (u < 0.35)
					col = ramp_hot.rgb;
				else if (u < 0.65)
					col = ramp_mid.rgb;
				else
					col = ramp_edge.rgb;
			}
		}
	}

	if (!hit) {
		discard;
	}
	COLOR = vec4(col * COLOR.rgb, 1.0);
}
