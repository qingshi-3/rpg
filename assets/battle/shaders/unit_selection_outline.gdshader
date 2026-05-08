shader_type canvas_item;

uniform bool outline_enabled = false;
uniform vec4 outline_color : source_color = vec4(1.0, 0.92, 0.34, 1.0);
uniform float outline_width : hint_range(0.0, 6.0, 0.25) = 2.0;

void fragment() {
    vec4 texture_color = texture(TEXTURE, UV);
    vec4 base_color = texture_color * COLOR;

    if (outline_enabled && outline_width > 0.0) {
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
