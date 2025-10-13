shader_type canvas_item;

uniform vec2 scroll_offset = vec2(0.0, 0.0);

void fragment() {
    vec2 uv = UV + scroll_offset;
    COLOR = texture(TEXTURE, fract(uv));
}