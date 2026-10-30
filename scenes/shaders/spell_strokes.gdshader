// Straight strokes of light, iron and dark as pixel art on the game's 2 px grid: every line the trap and
// star spells draw (scripts/SpellStrokes.cs) - a falling star's trail, a shooting star and the line it
// cut, the Constellation's web, Hollow Star's tether and its thrown shards, Gale Blade's Thunderhead,
// the Lattice's arcs and Iron Palisade's rail.
//
// They were DrawLine and DrawPolyline: a rotated quad with a smooth edge at any angle but the four
// square ones, and translucent besides - the vector look the Bonelight contract rules out
// (.ai/art-direction.md sections 1 and 3). So, as in riptide_wave.gdshader and stormsweep_lash, every
// fragment snaps itself to the WORLD's 2 px cell first and only then measures that cell against its
// stroke. A stroke at any angle is a staircase of square, upright art pixels, the same pixels from
// frame to frame however the thing moves, and it meets every other cell-drawn part on one grid.
//
// WHAT A STROKE IS. One quad per stroke, its row of the data texture in the quad's UV.x. Across the
// segment A..B, by the cell centre's distance from it:
//   spine  within `spine` px (one art pixel's width by default) - the hottest tone
//   body   within the half width, which runs from `half_a` at A to `half_b` at B (a trail tapers)
//   glow   up to `glow` px past the body, as an ordered dither of `density` - the only soft edge there
//          is, and it is a pattern of whole opaque pixels, never a wash
// Each is a TONE, an index into six colours the owner sets (an element ramp's core, hot, mid and edge,
// then two darks). With `cool` the tones step one stop down the ramp at 30% and at 65% of the way to B,
// so a trail is white-hot at the head and cools to its edge colour behind; with `lit` the outer body
// pixel on the key side (upper left, art-direction section 2) is one stop lighter and on the far side
// one stop darker, which is all an iron bar needs to read as round. `tail` dissolves the last share of
// the stroke toward B by dither, `dissolve` the whole of it, and `flash` turns the spine white-hot.
//
// THE NUMBERS ARE THE OWNER'S. A and B and the widths arrive in world px from the same values the
// owner's hit tests read (the web's LinkReach, a streak's half width, the rail's stakes); nothing here
// is drawn past half width + glow, and the owners keep that inside what they test.
//
// THE DATA ARRIVES AS A TEXTURE, as in stormsweep_lash.gdshader: a float image, four texels a row,
// rewritten in place every frame, so nothing is allocated on the C# side:
//   texel 0   A.xy, B.xy (world px)
//   texel 1   half_a, half_b, glow, density
//   texel 2   spine tone, body tone, glow tone, flags (1 cool, 2 lit, 4 hollow: the glow alone, a
//             dithered ring from half width outward - the Lattice's halo on a linked body)
//   texel 3   tail, dissolve, flash, spine (px; 0 is one art pixel)
// COLOR arrives as the quad's vertex colour times the canvas's modulate: a Legendary's gold or a card's
// tint, multiplied over the tone. Nothing is blended and nothing is antialiased.
shader_type canvas_item;
render_mode unshaded;

uniform sampler2D stroke_data : filter_nearest, repeat_disable;
uniform vec4 tone0 : source_color = vec4(1.0);
uniform vec4 tone1 : source_color = vec4(1.0);
uniform vec4 tone2 : source_color = vec4(1.0);
uniform vec4 tone3 : source_color = vec4(1.0);
uniform vec4 tone4 : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform vec4 tone5 : source_color = vec4(0.020, 0.027, 0.047, 1.0);

const float GRID = 2.0;
const vec2 KEY = vec2(-0.70710678, -0.70710678);

varying vec2 world;
varying flat int row;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	row = int(UV.x);
}

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

vec3 tone(int i) {
	if (i <= 0)
		return tone0.rgb;
	if (i == 1)
		return tone1.rgb;
	if (i == 2)
		return tone2.rgb;
	if (i == 3)
		return tone3.rgb;
	if (i == 4)
		return tone4.rgb;
	return tone5.rgb;
}

void fragment() {
	vec4 t0 = texelFetch(stroke_data, ivec2(0, row), 0);
	vec4 t1 = texelFetch(stroke_data, ivec2(1, row), 0);
	vec4 t2 = texelFetch(stroke_data, ivec2(2, row), 0);
	vec4 t3 = texelFetch(stroke_data, ivec2(3, row), 0);

	vec2 cell = floor(world / GRID);
	vec2 p = (cell + 0.5) * GRID;
	vec2 a = t0.xy;
	vec2 ab = t0.zw - a;
	float len2 = dot(ab, ab);
	float s = len2 > 0.0001 ? clamp(dot(p - a, ab) / len2, 0.0, 1.0) : 0.0;
	vec2 off = p - (a + ab * s);
	float d = length(off);
	float half_w = mix(t1.x, t1.y, s);
	float b = bayer4(cell);

	// The tail dissolving toward B, and the whole stroke going out.
	float gone = t3.y;
	if (t3.x > 0.0) {
		gone = max(gone, (s - (1.0 - t3.x)) / max(t3.x, 0.01));
	}
	int flags = int(t2.w + 0.5);
	int steps = (flags & 1) != 0 ? (s > 0.65 ? 2 : (s > 0.3 ? 1 : 0)) : 0;
	float spine = t3.w > 0.0 ? t3.w : GRID * 0.5;

	// Godot forbids a return in fragment(), so the bands are one chain that sets `keep`.
	bool keep = b >= gone;
	int t = 0;
	if ((flags & 4) != 0 && d <= half_w) {
		keep = false;
	} else if (d <= spine) {
		t = int(t2.x + 0.5) + steps;
		if (t3.z > 0.5)
			t = 0;
	} else if (d <= half_w) {
		t = int(t2.y + 0.5) + steps;
		if ((flags & 2) != 0 && d > half_w - GRID) {
			float facing = dot(off / max(d, 0.001), KEY);
			if (facing > 0.3)
				t -= 1;
			else if (facing < -0.3)
				t += 1;
		}
		if (t3.z > 0.5)
			t = max(t - 1, 0);
	} else if (d <= half_w + t1.z && b < t1.w) {
		t = int(t2.z + 0.5) + steps;
	} else {
		keep = false;
	}
	if (!keep) {
		discard;
	}
	COLOR = vec4(tone(clamp(t, 0, 5)) * COLOR.rgb, 1.0);
}
