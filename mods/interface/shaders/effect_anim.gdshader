shader_type canvas_item;
render_mode blend_premul_alpha;

uniform float frame_index : hint_range(0.0, 1024.0) = 0.0;
uniform float frame_count : hint_range(1.0, 1024.0) = 5.0;

void fragment() {
	float frame_width = 1.0 / frame_count;
	float frame_u = frame_index * frame_width;
	vec2 uv = UV;
	uv.x = uv.x * frame_width + frame_u;

	vec4 color = texture(TEXTURE, uv) * COLOR;

	float luma = dot(color.rgb, vec3(0.299, 0.587, 0.114));
	color.a *= luma;

#ifdef ALPHA_CLIP
	if (color.a < 0.001)
		discard;
#endif

	COLOR = color;
}
