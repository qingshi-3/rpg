shader_type canvas_item;

uniform vec4 base_outline_color : source_color = vec4(0.03, 0.03, 0.03, 0.78);
uniform float base_outline_width : hint_range(0.0, 3.0, 0.25) = 1.0;
uniform bool active_outline_enabled = false;
uniform vec4 active_outline_color : source_color = vec4(1.0, 0.92, 0.34, 1.0);
uniform float active_outline_width : hint_range(0.0, 6.0, 0.25) = 2.0;

void fragment() {
    vec4 texture_color = texture(TEXTURE, UV);
    vec4 base_color = COLOR;

    vec4 outline_color = base_outline_color;
    float outline_width = base_outline_width;
    if (active_outline_enabled && active_outline_color.a > 0.0 && active_outline_width > 0.0) {
        outline_color = active_outline_color;
        outline_width = active_outline_width;
    }

    if (outline_width > 0.0 && outline_color.a > 0.0) {
        vec2 texel = TEXTURE_PIXEL_SIZE * outline_width;
        float neighbor_alpha = 0.0;
        neighbor_alpha = max(neighbor_alpha, texture(TEXTURE, UV + vec2(texel.x, 0.0)).a);
        neighbor_alpha = max(neighbor_alpha, texture(TEXTURE, UV + vec2(-texel.x, 0.0)).a);
        neighbor_alpha = max(neighbor_alpha, texture(TEXTURE, UV + vec2(0.0, texel.y)).a);
        neighbor_alpha = max(neighbor_alpha, texture(TEXTURE, UV + vec2(0.0, -texel.y)).a);
        neighbor_alpha = max(neighbor_alpha, texture(TEXTURE, UV + vec2(texel.x, texel.y)).a);
        neighbor_alpha = max(neighbor_alpha, texture(TEXTURE, UV + vec2(-texel.x, texel.y)).a);
        neighbor_alpha = max(neighbor_alpha, texture(TEXTURE, UV + vec2(texel.x, -texel.y)).a);
        neighbor_alpha = max(neighbor_alpha, texture(TEXTURE, UV + vec2(-texel.x, -texel.y)).a);

        float outline_alpha = clamp(neighbor_alpha - texture_color.a, 0.0, 1.0) * outline_color.a;
        vec4 final_outline = vec4(outline_color.rgb, outline_alpha);
        COLOR = mix(final_outline, base_color, base_color.a);
    } else {
        COLOR = base_color;
    }
}
