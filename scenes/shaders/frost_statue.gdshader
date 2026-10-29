// Frost Shard's Ice Statues (scripts/FrostShatter.cs): a corpse's own frame, frozen where it fell,
// redrawn in the Ice ramp.
//
// The statue was the frame drawn with a multiply of (0.9, 1.2, 1.6) and a flash of (1.6, 1.8, 2.0): an
// over-bright tint over the corpse's own colours, which gives colours that are on no ramp. Here each of
// the frame's texels is mapped by its brightness onto the four Ice stops, as element_ramp.gdshader remaps
// a sheet into another element (.ai/art-direction.md section 3: "One drawn burst can serve every element
// by remapping its ramp") - so the statue is still exactly the corpse's pixels, on the corpse's own grid,
// in ice. The drawing call's colour carries two numbers, since one node draws every statue: red is how
// far it has frosted over (its first tenth of a second, a dissolve through the Bayer matrix on the
// frame's own texels), green how near it is to breaking (the stops climb toward white-hot).
shader_type canvas_item;
render_mode unshaded;

const vec3 CORE = vec3(0.957, 0.996, 1.0);   // ice core #F4FEFF
const vec3 HOT = vec3(0.784, 0.957, 1.0);    // ice hot #C8F4FF
const vec3 MID = vec3(0.498, 0.863, 0.973);  // ice mid #7FDCF8
const vec3 EDGE = vec3(0.180, 0.498, 0.769); // ice edge #2E7FC4

varying vec4 tint;

void vertex() {
	tint = COLOR;
}

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

void fragment() {
	vec4 src = texture(TEXTURE, UV);
	if (src.a < 0.5)
		discard;
	float frost = tint.r;
	float flash = tint.g;
	// The frame's own texel, so the dissolve walks the art's pixels, not the screen's.
	vec2 texel = floor(UV / TEXTURE_PIXEL_SIZE);
	if (frost < bayer4(texel)) {
		COLOR = vec4(src.rgb, 1.0);
	} else {
		// The horde is drawn dark (its occlusion, shade and deep stops), so its brightness is spread over
		// the four ice stops from the darkest it uses to about the brightest, not from black to white:
		// otherwise every body came out one flat edge-blue.
		float l = clamp((dot(src.rgb, vec3(0.299, 0.587, 0.114)) - 0.03) / 0.3, 0.0, 1.0) + flash * 0.6;
		vec3 c = EDGE;
		if (l > 0.8)
			c = CORE;
		else if (l > 0.45)
			c = HOT;
		else if (l > 0.2)
			c = MID;
		COLOR = vec4(c, 1.0);
	}
}
