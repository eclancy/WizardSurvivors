// A fade by dither, never by translucency: the material the trap and star spells' own canvases draw
// through (Gravewell, Glacial Spike's splinters, Iron Palisade, Hollow Star, Starfall, the Familiar,
// Mirefoot's wisps, Gale Blade's Thunderhead, the Lattice - scripts/SpellInk.cs makes it).
//
// The Bonelight contract allows one gradient, ordered dither (.ai/art-direction.md section 3), and no
// translucent wash. Those canvases already draw opaque ramp colours on the 2 px art grid; what they
// faded, they faded with alpha - a mote going out at 40% over whatever was under it, a pit filling back
// in as a ghost of itself. Here the alpha a draw call carries is read as COVERAGE instead: each art
// pixel is either drawn, opaque, in its own colour, or not drawn at all, by a 4x4 Bayer threshold. So a
// fade dissolves - the thing thins out pixel by pixel - and nothing ever mixes with what is beneath it.
//
// WHICH PIXEL. `by_texel` false (rects, the stamped cells): the WORLD's 2 px cell, so the pattern stays
// put on the ground under a moving mote, as frost_patch.gdshader's and mire_surface's do. `by_texel`
// true (a sprite drawn at x2): the sprite's own texel, so a dissolving pit or a blinking hound loses
// whole art pixels and never cuts one in half, wherever it stands.
//
// The node's modulate (a Legendary's gold) is kept: COLOR arrives as texture x modulate and only its
// alpha is replaced. Nothing here allocates; one material is shared by every canvas of a kind.
shader_type canvas_item;
render_mode unshaded;

uniform bool by_texel = false;

const float GRID = 2.0;

varying vec2 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

void fragment() {
	vec2 cell = by_texel ? floor(UV / TEXTURE_PIXEL_SIZE) : floor(world / GRID);
	float a = COLOR.a;
	if (a < 0.02 || bayer4(cell) >= a) {
		discard;
	}
	COLOR.a = 1.0;
}
