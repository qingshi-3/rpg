shader_type canvas_item;
render_mode unshaded;

uniform float hovered_region_id = 0.0;
uniform float selected_region_id = 0.0;
uniform float context_city_id = 0.0;
uniform vec4 player_color : source_color = vec4(0.31, 0.68, 0.91, 1.0);
uniform vec4 hostile_color : source_color = vec4(0.90, 0.37, 0.27, 1.0);
uniform vec4 neutral_color : source_color = vec4(0.82, 0.74, 0.47, 1.0);
uniform sampler2D region_mask : filter_nearest, repeat_disable;
uniform sampler2D region_metadata : filter_nearest, repeat_disable;
uniform vec2 mask_pixel_size = vec2(1.0);
uniform vec2 mask_uv_origin = vec2(0.0);
uniform vec2 mask_uv_size = vec2(1.0);

float region_id(vec2 uv) {
	return floor(texture(region_mask, uv).r * 255.0 + 0.5);
}

float id_gate(float id, float target_id) {
	return step(0.5, target_id) * (1.0 - step(0.25, abs(id - target_id)));
}

vec2 metadata_for_id(float id) {
	float lookup_u = (id + 0.5) / 256.0;
	return floor(texture(region_metadata, vec2(lookup_u, 0.5)).rg * 255.0 + 0.5);
}

float differs(float center_id, vec2 mask_uv, vec2 offset) {
	return step(0.25, abs(region_id(mask_uv + offset) - center_id));
}

void fragment() {
	vec2 mask_uv = mask_uv_origin + UV * mask_uv_size;
	float id = region_id(mask_uv);
	if (id < 0.5) {
		discard;
	}

	vec2 metadata = metadata_for_id(id);
	float player_gate = id_gate(metadata.r, 1.0);
	float hostile_gate = id_gate(metadata.r, 2.0);
	float context_gate = id_gate(metadata.g, context_city_id);
	float hover_gate = id_gate(id, hovered_region_id);
	float selection_gate = id_gate(id, selected_region_id);
	float active_gate = max(hover_gate, selection_gate);
	vec3 faction_color = mix(neutral_color.rgb, player_color.rgb, player_gate);
	faction_color = mix(faction_color, hostile_color.rgb, hostile_gate);

	vec2 pixel = mask_pixel_size;
	float near_edge = max(max(differs(id, mask_uv, vec2(pixel.x, 0.0)), differs(id, mask_uv, vec2(-pixel.x, 0.0))),
		max(differs(id, mask_uv, vec2(0.0, pixel.y)), differs(id, mask_uv, vec2(0.0, -pixel.y))));
	float soft_edge = max(max(differs(id, mask_uv, vec2(pixel.x * 2.0, 0.0)), differs(id, mask_uv, vec2(-pixel.x * 2.0, 0.0))),
		max(differs(id, mask_uv, vec2(0.0, pixel.y * 2.0)), differs(id, mask_uv, vec2(0.0, -pixel.y * 2.0))));

	float fill_alpha = max(selection_gate * 0.18, max(hover_gate * 0.11, context_gate * (1.0 - active_gate) * 0.018));
	float edge_alpha = 0.12 + context_gate * 0.18 + hover_gate * 0.50 + selection_gate * 0.66;
	float halo_alpha = 0.018 + context_gate * 0.035 + hover_gate * 0.10 + selection_gate * 0.14;
	float halo_only = max(soft_edge - near_edge, 0.0);
	float alpha = max(fill_alpha, max(near_edge * edge_alpha, halo_only * halo_alpha));
	vec3 matte_color = mix(vec3(0.78), faction_color, 0.18 + active_gate * 0.10);
	vec3 border_color = mix(faction_color, vec3(0.92), 0.12 + active_gate * 0.20);
	vec3 output_color = mix(matte_color, border_color, clamp(near_edge + halo_only * 0.55, 0.0, 1.0));
	COLOR = vec4(output_color, alpha);
}
