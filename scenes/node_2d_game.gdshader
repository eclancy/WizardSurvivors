shader_type canvas_item;

uniform vec2 scroll_offset = vec2(0.0, 0.0);
uniform float scroll_scale = 1.0;
uniform float texture_scale = 1.0;

void fragment() {
    vec2 uv = (UV * texture_scale) + (scroll_offset * scroll_scale);
    COLOR = texture(TEXTURE, fract(uv));
}