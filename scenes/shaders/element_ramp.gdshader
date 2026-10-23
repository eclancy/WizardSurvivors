// An element path's repaint (scripts/ElementRampMaterial.cs): a sprite drawn in one element's
// emissive ramp, redrawn in another's.
//
// .ai/art-direction.md section 3: "One drawn burst can serve every element by remapping its ramp."
// Every effect sheet is drawn in four stops - core, hot, mid, edge - so each pixel's brightness says
// which stop it is, and that stop is swapped for the same stop of the new element. Cinderbreath's
// flame is exactly the four Fire stops (#FFF4D0, #FFC848, #E87A1A, #B8381A at luminance 0.95, 0.79,
// 0.56, 0.36), so a branch into Darkness is the same tongue of flame in Darkness's four. No gradient
// is introduced: the result is still four flat colours, as pixel art must be. Anything darker than
// an edge (a sheet's smoke or outline) takes the edge at half light.
shader_type canvas_item;

uniform vec4 core : source_color = vec4(1.0);
uniform vec4 hot : source_color = vec4(1.0);
uniform vec4 mid : source_color = vec4(0.5);
uniform vec4 edge : source_color = vec4(0.25);

void fragment() {
	vec4 src = texture(TEXTURE, UV);
	float l = dot(src.rgb, vec3(0.299, 0.587, 0.114));
	vec3 c = edge.rgb * 0.45;
	if (l > 0.86)
		c = core.rgb;
	else if (l > 0.66)
		c = hot.rgb;
	else if (l > 0.42)
		c = mid.rgb;
	else if (l > 0.18)
		c = edge.rgb;
	// The node's modulate still fades it: only its alpha is kept, since its colour is the thing
	// being replaced.
	COLOR = vec4(c, src.a * COLOR.a);
}
