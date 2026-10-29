// Wisp Trail (scripts/WardWisps.cs, Kindled Ward's level 4): the small lights a drifting ward sheds,
// each hanging low where it was shed and searing whatever walks into it - as pixel art on the game's
// 2 px grid.
//
// Each wisp was two translucent squashed DrawCircles for the glow pooled under it - a wash, which doubled
// into a brighter lens wherever two wisps overlapped, as nearly all of them do along a trail - and two
// more for the mote. Here a wisp is one quad: every 2 px cell is shaded opaque from the ward's ramp, with
// Bayer dithering the only gradient (.ai/art-direction.md sections 1 and 3).
//
// THE RADIUS IS THE HIT. `radius` is the reach WardWisps.Sear tests every half second. What lies there is
// the stuff that sears, laid out to it (section 3, "a lasting zone is the stuff that hurts"): flecks of
// the ward's light settled on the floor, thickest under the mote and thinning out, the last of them on
// the rim and none past it - twinkling, re-chosen a few times a second, never a disc. Squashed into the
// floor's three-quarter view as the craters are. Over it the mote itself bobs a little off the ground.
//
// PER WISP, IN THE VERTEX COLOUR, so every wisp of the trail shares one material and batches:
//   r  how much of it there is: born (it swells in) x going out (it gutters)
//   g  its radius / 128
//   b  its seed, 0..1
//   a  how far it is from going out (1 burning, 0 out), which the mote shrinks with
shader_type canvas_item;
render_mode unshaded;

uniform vec4 tone_core : source_color = vec4(1.0, 1.0, 1.0, 1.0);        // light core #FFFFFF
uniform vec4 tone_hot : source_color = vec4(1.0, 0.973, 0.847, 1.0);     // light hot #FFF8D8
uniform vec4 tone_mid : source_color = vec4(1.0, 0.941, 0.659, 1.0);     // light mid #FFF0A8
uniform vec4 tone_edge : source_color = vec4(1.0, 0.776, 0.235, 1.0);    // light edge #FFC63C
uniform float time = 0.0;

const float GRID = 2.0;
const float SQUASH = 0.84;
// The mote's float above the ground, and the quad's least half height to hold it.
const float LIFT = 14.0;

varying vec2 local;
varying flat vec4 params;

void vertex() {
	params = COLOR;
	float r = COLOR.g * 128.0;
	local = (UV - vec2(0.5)) * 2.0 * vec2(r, max(r * SQUASH, LIFT));
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

void fragment() {
	float amount = params.r;
	float radius = params.g * 128.0;
	float s = params.b;
	float burning = params.a;
	vec2 cell = floor(local / GRID);
	vec2 p = cell * GRID + GRID * 0.5;
	float b = bayer4(cell);
	float beat = s * 13.0;
	vec3 col = vec3(0.0);
	bool hit = false;

	// The mote, bobbing just over the ground: a white heart in a ring of hot light.
	vec2 mote = vec2(sin(time * 2.1 + beat * 9.0) * 2.0, -6.0 - 3.0 * sin(time * 3.3 + beat * 5.0));
	float flick = hash12(vec2(floor(time / 0.07), 1.0), s) > 0.5 ? 1.0 : 0.0;
	float m = length(p - mote);
	float size = 0.6 + 0.4 * burning;
	if (m <= 1.6) { col = tone_core.rgb; hit = true; }
	else if (m <= 3.4 * size + flick) { col = tone_hot.rgb; hit = true; }
	else if (m <= 4.8 * size + flick && b < 0.5) { col = tone_mid.rgb; hit = true; }
	if (hit && amount < b)
		hit = false;

	// The light it has laid on the floor, out to the radius it sears.
	if (!hit) {
		vec2 q = vec2(p.x, p.y / SQUASH);
		float d = length(q) / max(radius, 1.0);
		if (d <= 1.0 - GRID * 0.5 / max(radius, 1.0)) {
			// Re-chosen a few times a second, each cell on its own beat, so the flecks twinkle.
			float clock = floor(time * 6.0 + hash12(cell, s + 5.0) * 4.0);
			float h = hash12(cell + vec2(clock * 3.0, 0.0), s);
			float density = (d < 0.85 ? 0.05 + 0.2 * (1.0 - d) * (1.0 - d) : 0.13) * amount;
			if (h < density) {
				col = d < 0.3 ? tone_hot.rgb : (d < 0.7 ? tone_mid.rgb : tone_edge.rgb);
				hit = true;
			}
		}
	}

	if (!hit)
		discard;
	COLOR = vec4(col, 1.0);
}
