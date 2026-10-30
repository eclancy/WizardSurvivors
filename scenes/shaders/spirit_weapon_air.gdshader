// Spiritual Weapon's air and floor canvases (scripts/SpiritualWeapon.cs): what makes everything they draw
// opaque pixel art on the game's 2 px grid.
//
// The weapons are generated sheets (tools/art/spirit_weapons.py, one cell per heading, drawn at x2 on the
// grid) and pass the Bonelight bar as drawn. What did not was how they came and went and what they left:
// a weapon faded in and out through its modulate's alpha, its afterimages were the sheet tinted violet at
// 22% and 38%, the slash was translucent DrawLines, the motes and the shadow were rects faded by alpha -
// translucent washes in a hard-edged, nearest-filtered game (.ai/art-direction.md section 3: "fading is by
// dither or by stepping down the ramp").
//
// So every draw on these canvases is still made the way it was - a sheet cell or a rect, a colour and an
// alpha - and this shader turns the ALPHA into COVERAGE: a cell is drawn, opaque, while its Bayer
// threshold (of the world's 2 px cell, so the dither is the world's) is under it, and not at all after.
// A weapon dissolves in and out; an afterimage is a stipple of the weapon's own shape; the slash and the
// motes cool down their ramp and break up rather than going transparent.
//
// `silhouette`: the afterimage canvas draws the sheet's shape filled with the colour it is given (Arcane's
// mid and hot) rather than the sheet's colours multiplied by it, which muddied the gold haft.
shader_type canvas_item;
render_mode unshaded;

uniform bool silhouette = false;

const float GRID = 2.0;

varying vec2 world;
varying vec4 tint;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	// The draw's own colour and alpha, before any texture is multiplied in.
	tint = COLOR;
}

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	if (tex.a < 0.5)
		discard;
	if (tint.a < bayer4(floor(world / GRID)))
		discard;
	COLOR = vec4(silhouette ? tint.rgb : tex.rgb * tint.rgb, 1.0);
}
