shader_type canvas_item;
render_mode blend_disabled;

uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;
uniform float pixel_size : hint_range(0.1, 1000.0) = 1.0;

void fragment() {
	vec2 pixel_scale = SCREEN_PIXEL_SIZE * pixel_size;
	vec2 pixel_uv = floor(SCREEN_UV / pixel_scale) * pixel_scale;
	COLOR = texture(screen_texture, pixel_uv);
}
