shader_type canvas_item;
render_mode blend_premul_alpha;

uniform float pixel_size : hint_range(0.1, 1000.0) = 1.0;

void fragment() {
	vec2 pixel_scale = SCREEN_PIXEL_SIZE * pixel_size;
	vec2 pixel_uv = floor(SCREEN_UV / pixel_scale) * pixel_scale;

	vec4 color = texture(TEXTURE, pixel_uv) * COLOR;

#ifdef ALPHA_CLIP
	if (color.a < 0.001)
		discard;
#endif

	COLOR = color;
}
