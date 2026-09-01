shader_type canvas_item;

// Temporary placeholder-art shader: gives procedurally-drawn shapes a bit of visual life
// (a subtle brightness/alpha pulse) until real pixel art replaces them. Shared across every
// PlaceholderShape instance in the project.

uniform float pulse_speed : hint_range(0.1, 10.0) = 3.0;
uniform float pulse_strength : hint_range(0.0, 1.0) = 0.35;

void fragment()
{
	float pulse = 1.0 + sin(TIME * pulse_speed) * pulse_strength;
	// COLOR must be *multiplied* by the texture, not replaced by it. PlaceholderShape draws
	// untextured primitives, so TEXTURE is the 1x1 white fallback and the real colour arrives in
	// COLOR from _Draw(). Assigning `COLOR = texture(...)` threw that away and rendered every
	// placeholder shape in the game as an opaque white blob regardless of its ShapeColor.
	COLOR *= texture(TEXTURE, UV);
	COLOR.rgb *= pulse;
}
