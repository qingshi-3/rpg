shader_type canvas_item;

uniform vec2 focus_uv = vec2(0.5, 0.5);
uniform vec2 viewport_size = vec2(1280.0, 720.0);
uniform vec4 shadow_color : source_color = vec4(0.0, 0.0, 0.0, 0.38);
uniform float inner_radius : hint_range(0.02, 0.6, 0.01) = 0.18;
uniform float outer_radius : hint_range(0.1, 1.2, 0.01) = 0.62;
uniform float center_alpha : hint_range(0.0, 0.5, 0.01) = 0.0;

void fragment() {
    float aspect = max(viewport_size.x / max(viewport_size.y, 1.0), 0.01);
    vec2 scaled_uv = vec2(UV.x * aspect, UV.y);
    vec2 scaled_focus = vec2(focus_uv.x * aspect, focus_uv.y);
    float distance_to_focus = distance(scaled_uv, scaled_focus);
    float radius_end = max(outer_radius, inner_radius + 0.001);
    float shadow_amount = smoothstep(inner_radius, radius_end, distance_to_focus);
    float alpha = mix(center_alpha, shadow_color.a, shadow_amount);
    COLOR = vec4(shadow_color.rgb, alpha);
}
