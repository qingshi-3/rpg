shader_type canvas_item;
render_mode unshaded;

uniform vec4 player_control_color : source_color = vec4(0.31, 0.68, 0.87, 1.0);
uniform vec4 enemy_control_color : source_color = vec4(0.85, 0.38, 0.30, 1.0);
uniform vec4 neutral_color : source_color = vec4(0.71, 0.64, 0.44, 1.0);
uniform float fill_alpha = 0.055;
uniform float city_boundary_alpha = 0.24;
uniform float province_boundary_alpha = 0.68;
uniform sampler2D region_mask : filter_nearest, repeat_disable;
uniform sampler2D region_metadata : filter_nearest, repeat_disable;
uniform vec2 mask_pixel_size = vec2(1.0);
uniform float metadata_width = 1.0;

float region_id(vec2 uv) {
	vec3 encoded = floor(texture(region_mask, uv).rgb * 255.0 + 0.5);
	return encoded.r + encoded.g * 256.0 + encoded.b * 65536.0;
}

vec3 metadata_for_id(float id) {
	float lookup_u = (id + 0.5) / metadata_width;
	vec4 metadata = texture(region_metadata, vec2(lookup_u, 0.5));
	return vec3(
		floor(metadata.r * 255.0 + 0.5),
		floor(metadata.g * 255.0 + 0.5),
		metadata.b);
}

float differs(float center_id, vec2 uv) {
	return step(0.25, abs(region_id(uv) - center_id));
}

float province_differs(float center_province, vec2 uv) {
	float neighbor_id = region_id(uv);
	float neighbor_province = neighbor_id < 0.5 ? 0.0 : metadata_for_id(neighbor_id).g;
	return step(0.25, abs(neighbor_province - center_province));
}

void fragment() {
	float id = region_id(UV);
	if (id < 0.5) {
		discard;
	}

	vec3 metadata = metadata_for_id(id);
	float control_code = metadata.r;
	float province_code = metadata.g;
	float player_gate = 1.0 - step(0.25, abs(control_code - 1.0));
	float hostile_gate = 1.0 - step(0.25, abs(control_code - 2.0));
	vec3 province_color = mix(neutral_color.rgb, player_control_color.rgb, player_gate);
	province_color = mix(province_color, enemy_control_color.rgb, hostile_gate);
	vec3 restrained_fill = mix(vec3(0.72), province_color, 0.20 + metadata.b * 0.06);

	vec2 pixel = mask_pixel_size;
	float city_edge = max(max(differs(id, UV + vec2(pixel.x, 0.0)), differs(id, UV - vec2(pixel.x, 0.0))),
		max(differs(id, UV + vec2(0.0, pixel.y)), differs(id, UV - vec2(0.0, pixel.y))));
	float province_edge = max(max(province_differs(province_code, UV + vec2(pixel.x, 0.0)), province_differs(province_code, UV - vec2(pixel.x, 0.0))),
		max(province_differs(province_code, UV + vec2(0.0, pixel.y)), province_differs(province_code, UV - vec2(0.0, pixel.y))));
	float soft_edge = max(max(differs(id, UV + vec2(pixel.x * 2.0, 0.0)), differs(id, UV - vec2(pixel.x * 2.0, 0.0))),
		max(differs(id, UV + vec2(0.0, pixel.y * 2.0)), differs(id, UV - vec2(0.0, pixel.y * 2.0))));
	float halo = max(soft_edge - city_edge, 0.0);

	float alpha = max(fill_alpha, city_edge * city_boundary_alpha);
	alpha = max(alpha, province_edge * province_boundary_alpha);
	alpha = max(alpha, halo * city_boundary_alpha * 0.24);
	vec3 city_border = mix(province_color, vec3(0.88), 0.40);
	vec3 province_border = mix(province_color, vec3(0.96), 0.16);
	vec3 output_color = mix(restrained_fill, city_border, clamp(city_edge + halo * 0.35, 0.0, 1.0));
	output_color = mix(output_color, province_border, province_edge);
	COLOR = vec4(output_color, alpha);
}
