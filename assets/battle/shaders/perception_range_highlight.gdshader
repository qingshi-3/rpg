shader_type canvas_item;

uniform float pulse_speed : hint_range(0.0, 6.0, 0.1) = 1.15;
uniform float pulse_strength : hint_range(0.0, 0.45, 0.01) = 0.12;
uniform float edge_glow : hint_range(0.0, 2.0, 0.01) = 0.72;
uniform float edge_alpha_boost : hint_range(0.0, 0.35, 0.01) = 0.08;
uniform float scanline_strength : hint_range(0.0, 0.5, 0.01) = 0.14;
uniform float scanline_scale : hint_range(2.0, 40.0, 0.5) = 14.0;

void fragment() {
    vec4 base = texture(TEXTURE, UV) * COLOR;
    if (base.a <= 0.001) {
        COLOR = vec4(0.0);
    } else {
        float pulse = sin(TIME * pulse_speed) * 0.5 + 0.5;
        float edge_band = smoothstep(0.02, 0.12, base.a) * (1.0 - smoothstep(0.12, 0.32, base.a));
        float interior = smoothstep(0.12, 0.5, base.a);
        float scan = sin((UV.x - UV.y) * scanline_scale + TIME * pulse_speed * 1.7) * 0.5 + 0.5;

        vec3 rgb = base.rgb;
        rgb += base.rgb * edge_band * edge_glow;
        rgb += base.rgb * interior * scan * scanline_strength;

        float alpha = base.a * (1.0 + pulse * pulse_strength) + edge_band * edge_alpha_boost;
        COLOR = vec4(min(rgb, vec3(1.0)), clamp(alpha, 0.0, 1.0));
    }
}
