shader_type canvas_item;
render_mode blend_premul_alpha;

uniform sampler2D depth_mask : source_color, filter_linear;
uniform sampler2D albedo_map : source_color, filter_linear;

uniform float flicker_speed : hint_range(0.0, 100.0) = 5.0;
uniform float flicker_strength : hint_range(0.0, 1.0) = 0.3;
uniform float flicker_scale : hint_range(0.0, 10.0) = 1.0;

uniform bool use_time = true;

uniform vec4 day_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 morning_color : source_color = vec4(0.8207547, 0.6465379, 0.8050903, 1.0);
uniform vec4 night_color : source_color = vec4(0.0, 0.13673696, 0.20754719, 1.0);
uniform vec4 evening_color : source_color = vec4(0.81671757, 0.7075472, 0.9433962, 1.0);
uniform vec4 time_weights = vec4(0.0, 1.0, 0.0, 0.0);

uniform float light_intensity : hint_range(0.0, 10.0) = 2.0;
uniform float light_falloff : hint_range(0.0, 10.0) = 3.0;
uniform float light_radius_scale : hint_range(0.0, 10.0) = 1.0;

uniform vec2 light_positions[11];
uniform vec4 light_colors[11];
uniform float light_radii[11];
uniform float light_influence[11];
uniform int light_count;

varying vec2 world_pos;

float rand(vec2 co) {
	return fract(sin(dot(co, vec2(12.9898, 78.233))) * 43758.5453);
}

vec3 calculate_light_color(vec2 wp, out float total_influence) {
	vec3 combined_color = vec3(0.0, 0.0, 0.0);
	total_influence = 0.0;

	for (int i = 0; i < 11; i++) {
		if (i >= light_count)
			break;

		vec2 light_pos = light_positions[i];
		float light_radius = light_radii[i] * light_radius_scale;
		vec3 light_col = light_colors[i].rgb;

		float dist = distance(wp, light_pos);
		float influence = clamp(1.0 - dist / light_radius, 0.0, 1.0);
		// Avoid pow with negative base
		if (influence <= 0.0)
			continue;
		influence = pow(influence, light_falloff);

		float flicker_seed = light_pos.x * 1.357 + light_pos.y * 2.468 + float(i) * 3.579;

		float discrete_time = floor(TIME * flicker_speed * 3.0 + flicker_seed) / 3.0;

		float t1 = discrete_time * 2.0;
		float t2 = discrete_time * 3.0 + 1.2;
		float t3 = discrete_time * 4.0 + 2.4;

		float flicker1 = floor((sin(t1) * 0.5 + 0.5) * 4.0) / 3.0;
		float flicker2 = floor((sin(t2) * 0.3 + 0.5) * 3.0) / 2.0;
		float flicker3 = floor((sin(t3) * 0.2 + 0.5) * 3.0) / 2.0;

		float flicker = flicker1 * 0.5 + flicker2 * 0.3 + flicker3 * 0.2;

		float noise_seed = floor(TIME * flicker_speed * 2.0 + flicker_seed);
		float noise_val = rand(vec2(noise_seed, flicker_seed)) * 0.15;

		flicker = floor((flicker + noise_val) * 5.0) / 4.0;
		flicker = clamp(flicker, 0.0, 1.0);

		float flicker_factor = 0.7 + flicker * 0.6 * flicker_strength;
		flicker_factor = floor(flicker_factor * 8.0) / 7.0;

		influence *= flicker_factor;

		combined_color += light_col * influence * light_intensity * light_influence[i];
		total_influence += influence * light_intensity * light_influence[i];
	}

	return combined_color;
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec4 color = texture(TEXTURE, UV) * COLOR;
	float a = color.a;

	float depth = texture(depth_mask, UV).r;
	float albedo = texture(albedo_map, UV).r;

#ifdef ALPHA_CLIP
	if (color.a < 0.001)
		discard;
#endif

	if (!use_time) {
		COLOR = color;
	} else {
	    vec4 weights = time_weights;
    
	    vec4 time_color = morning_color * weights.x
	    	+ day_color * weights.y
	    	+ evening_color * weights.z
	    	+ night_color * weights.w;
    
	    float total_influence;
	    vec3 light_col = calculate_light_color(world_pos, total_influence);
	    total_influence = clamp(total_influence, 0.0, 1.0);
    
	    float light_infl = clamp(mix(light_col.r, 0.0, weights.y + weights.x + weights.z) * depth, 0.0, 1.0);
    
	    float pixel_luma = max(dot(color.rgb, vec3(0.299, 0.587, 0.114)), 0.01);
	    float target_luma = max(albedo, pixel_luma);
	    float lift = mix(1.0, target_luma / pixel_luma, light_infl);
    
	    vec3 lit_result = min(color.rgb * lift * light_col, 1.0);
    
	    COLOR.rgb = mix(color.rgb * time_color.rgb, lit_result, light_infl);
	    COLOR.a = a;
	}
}
