shader_type canvas_item;

// Temporary placeholder-art shader: gives procedurally-drawn shapes a bit of visual life
// (a subtle brightness/alpha pulse) until real pixel art replaces them. Shared across every
// PlaceholderShape instance in the project.

uniform float pulse_speed : hint_range(0.1, 10.0) = 3.0;
uniform float pulse_strength : hint_range(0.0, 1.0) = 0.35;

void fragment()
{
	float pulse = 1.0 + sin(TIME * pulse_speed) * pulse_strength;
	vec4 tex_color = texture(TEXTURE, UV);
	COLOR = tex_color;
	COLOR.rgb *= pulse;
	COLOR.a = tex_color.a;
}
