shader_type canvas_item;

uniform float pulse_speed : hint_range(0.0, 8.0, 0.1) = 3.2;
uniform float pulse_strength : hint_range(0.0, 0.75, 0.01) = 0.34;
uniform float glow_boost : hint_range(0.0, 1.0, 0.01) = 0.28;

void fragment() {
    float wave = sin(TIME * pulse_speed) * 0.5 + 0.5;
    vec3 rgb = min(COLOR.rgb * (1.0 + glow_boost * wave), vec3(1.0));
    float alpha = clamp(COLOR.a * (1.0 + pulse_strength * wave), 0.0, 1.0);
    COLOR = vec4(rgb, alpha);
}
