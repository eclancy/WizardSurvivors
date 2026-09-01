shader_type canvas_item;

// Renders the sprite as a flat white silhouette while a post-menu invincibility grace period is
// running. Applied as a material rather than via modulate because modulate multiplies (so it can
// darken or tint but never whiten), and because the player's modulate already carries the
// per-character tint and the Blur dodge flash - overwriting it would clobber both.

uniform float whiten : hint_range(0.0, 1.0) = 1.0;

void fragment()
{
	// COLOR arrives as texture(TEXTURE, UV) * MODULATE, so mixing only rgb preserves the
	// sprite's alpha and keeps transparent pixels transparent.
	COLOR.rgb = mix(COLOR.rgb, vec3(1.0), whiten);
}
