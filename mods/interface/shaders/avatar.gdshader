shader_type canvas_item;
render_mode blend_premul_alpha;

uniform int hide_mode : hint_range(0, 1) = 0;
uniform vec4 outline_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 invert_outline_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec2 pixel_size = vec2(0.001953125, 0.001953125);

void fragment() {
	vec4 color = texture(TEXTURE, UV) * COLOR;

#ifdef ALPHA_CLIP
	if (color.a < 0.001)
		discard;
#endif

	if (hide_mode > 0) {
		color.r = 0.0;
		color.g = 0.0;
		color.b = 0.0;
	}

	if (color.a > 0.99) {
		COLOR = color;
	} else {
		float max_alpha = 0.0;

		for (int x = -2; x <= 2; x++) {
			for (int y = -2; y <= 2; y++) {
				if (x == 0 && y == 0)
					continue;

				vec2 offset = vec2(float(x), float(y)) * pixel_size;
				float neighbor_alpha = texture(TEXTURE, UV + offset).a;
				max_alpha = max(max_alpha, neighbor_alpha);
			}
		}

		if (max_alpha > 0.99) {
			if (hide_mode > 0)
				COLOR = invert_outline_color;
			else
				COLOR = outline_color;
		} else {
			COLOR = color;
		}
	}
}
