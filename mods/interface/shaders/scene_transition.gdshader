shader_type canvas_item;
render_mode blend_premul_alpha;

uniform vec4 tint : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float noise_scale : hint_range(1.0, 100.0) = 20.0;
uniform float smoothness : hint_range(0.0, 0.5) = 0.1;
uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform bool invert_direction = false;
uniform float distortion : hint_range(0.0, 0.5) = 0.05;
uniform float hue_shift : hint_range(0.0, 1.0) = 0.2;
uniform float glow_intensity : hint_range(0.0, 1.0) = 0.3;

float random(vec2 uv) {
	return fract(sin(dot(uv, vec2(12.9898, 78.233))) * 43758.5453);
}

float perlin_noise(vec2 uv) {
	vec2 i = floor(uv);
	vec2 f = fract(uv);
	vec2 u = f * f * (3.0 - 2.0 * f);

	float a = random(i);
	float b = random(i + vec2(1.0, 0.0));
	float c = random(i + vec2(0.0, 1.0));
	float d = random(i + vec2(1.0, 1.0));

	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fractal_noise(vec2 uv, int octaves) {
	float value = 0.0;
	float amplitude = 0.5;
	float frequency = 1.0;

	for (int i = 0; i < 8; i++) {
		if (i >= octaves)
			break;
		value += perlin_noise(uv * frequency) * amplitude;
		amplitude *= 0.5;
		frequency *= 2.0;
	}

	return value;
}

vec3 hue_shift_func(vec3 color, float hue) {
	const vec3 k = vec3(0.57735, 0.57735, 0.57735);
	float cos_angle = cos(hue);
	return color * cos_angle + cross(k, color) * sin(hue) + k * dot(k, color) * (1.0 - cos_angle);
}

void fragment() {
	vec2 noise_uv = UV * noise_scale;
	float noise = fractal_noise(noise_uv, 3);

	float distorted_progress = progress + noise * distortion;

	float transition_mask;
	if (invert_direction)
		transition_mask = step(distorted_progress, noise);
	else
		transition_mask = step(noise, distorted_progress);

	float edge = smoothstep(0.0, smoothness, abs(noise - progress));
	float alpha = mix(transition_mask, 1.0 - transition_mask, edge);
	if (invert_direction)
		alpha = 1.0 - alpha;

	vec4 col = texture(TEXTURE, UV) * COLOR * tint;

	float glow = clamp(abs(noise - progress) / smoothness, 0.0, 1.0) * glow_intensity;
	col.rgb += glow * vec3(1.0, 0.8, 0.5) * col.a;

	if (abs(noise - progress) < smoothness * 2.0) {
		col.rgb = hue_shift_func(col.rgb, hue_shift);
	}

	col.a *= alpha;
	COLOR = col;
}
