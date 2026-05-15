shader_type canvas_item;

const int MAX_VISIBLE_CIRCLES = 32;

uniform vec4 unknown_color : source_color = vec4(0.015, 0.016, 0.018, 0.86);
uniform vec4 revealed_color : source_color = vec4(0.025, 0.030, 0.035, 0.42);
uniform sampler2D explored_mask;
uniform vec2 overlay_size = vec2(1.0, 1.0);
uniform vec4 map_rect = vec4(0.0, 0.0, 1.0, 1.0);
uniform int visible_circle_count = 0;
uniform vec2 explored_mask_texture_size = vec2(256.0, 256.0);
uniform vec4 visible_circles[MAX_VISIBLE_CIRCLES];
uniform float edge_softness = 5.0;

float SampleExploredMaskSmooth(vec2 uv, vec2 uv_step)
{
    float weight_sum = 0.0;
    float value_sum = 0.0;

    const int k_blur_half = 1;
    for (int y = -k_blur_half; y <= k_blur_half; y++)
    {
        for (int x = -k_blur_half; x <= k_blur_half; x++)
        {
            vec2 sample_offset = vec2(float(x), float(y)) * uv_step;
            float weight = (x == 0 && y == 0) ? 4.0 : (x == 0 || y == 0) ? 2.0 : 1.0;
            vec2 sample_uv = clamp(uv + sample_offset, vec2(0.0), vec2(1.0));
            value_sum += texture(explored_mask, sample_uv).r * weight;
            weight_sum += weight;
        }
    }

    return value_sum / max(weight_sum, 0.0001);
}

float SampleVisibleMaskSmooth(vec2 pixel)
{
    const int k_blur_half = 1;
    float value_sum = 0.0;
    float weight_sum = 0.0;

    for (int offset_y = -k_blur_half; offset_y <= k_blur_half; offset_y++)
    {
        for (int offset_x = -k_blur_half; offset_x <= k_blur_half; offset_x++)
        {
            vec2 sample_pixel = pixel + vec2(float(offset_x), float(offset_y));
            float sample_visible = 0.0;

            for (int index = 0; index < MAX_VISIBLE_CIRCLES; index++)
            {
                if (index >= visible_circle_count)
                {
                    break;
                }

                vec3 circle = visible_circles[index].xyz;
                float distance_to_circle = distance(sample_pixel, circle.xy);
                float circle_visible = 1.0 - smoothstep(circle.z - edge_softness, circle.z + edge_softness, distance_to_circle);
                sample_visible = max(sample_visible, circle_visible);
            }

            float weight = (offset_x == 0 && offset_y == 0) ? 4.0 : (offset_x == 0 || offset_y == 0) ? 2.0 : 1.0;
            value_sum += sample_visible * weight;
            weight_sum += weight;
        }
    }

    return value_sum / max(weight_sum, 0.0001);
}

void fragment() {
    vec2 pixel = UV * overlay_size;
    vec2 map_min = map_rect.xy;
    vec2 map_max = map_rect.xy + map_rect.zw;
    bool inside_map = pixel.x >= map_min.x && pixel.y >= map_min.y && pixel.x <= map_max.x && pixel.y <= map_max.y;
    vec4 fog_color = vec4(0.0);
    if (inside_map) {
        vec2 mask_uv = clamp((pixel - map_rect.xy) / max(map_rect.zw, vec2(1.0)), vec2(0.0), vec2(1.0));
        vec2 uv_step = vec2(1.0) / max(explored_mask_texture_size, vec2(1.0));
        float explored = SampleExploredMaskSmooth(mask_uv, uv_step);
        float visible_blur = SampleVisibleMaskSmooth(pixel);
        float visible_amount = visible_blur;
        float explored_amount = smoothstep(0.05, 0.95, explored);
        float blend = max(explored_amount, visible_amount);
        float visibility = smoothstep(0.0, 1.0, visible_amount);
        fog_color = mix(unknown_color, revealed_color, blend);
        fog_color.a *= 1.0 - visibility;
    }

    COLOR = fog_color;
}
