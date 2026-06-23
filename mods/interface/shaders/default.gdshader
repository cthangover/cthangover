shader_type canvas_item;
render_mode blend_premul_alpha;

uniform vec4 tint : source_color = vec4(1.0, 1.0, 1.0, 1.0);

void fragment() {
	vec4 color = texture(TEXTURE, UV) * COLOR * tint;

#ifdef ALPHA_CLIP
	if (color.a < 0.001)
		discard;
#endif

	COLOR = color;
}
