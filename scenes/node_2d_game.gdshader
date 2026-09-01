shader_type canvas_item;

uniform vec2 scroll_offset = vec2(0.0, 0.0);
uniform float scroll_scale = 0.0;
uniform float texture_scale = 1.0;
// When true the ground is drawn as a seamless world-space tile that scrolls 1:1
// with the camera (no aspect distortion). Otherwise a single atlas region /
// sheet is clamped to fill the view.
uniform bool tile = true;
uniform vec2 world_offset = vec2(0.0, 0.0);
uniform float tile_size = 64.0;

void fragment() {
    // Multiply into COLOR rather than assigning over it. COLOR arrives holding the node's
    // modulate, which is how StageEnvironmentCatalog tints each stage's ground; assigning
    // straight from the texture threw that away and every stage rendered its floor untinted.
    if (tile) {
        vec2 uv = fract((FRAGCOORD.xy + world_offset) / tile_size);
        COLOR *= texture(TEXTURE, uv);
    } else {
        vec2 uv = (UV * texture_scale) + (scroll_offset * scroll_scale);
        COLOR *= texture(TEXTURE, clamp(uv, vec2(0.0), vec2(1.0)));
    }
}
