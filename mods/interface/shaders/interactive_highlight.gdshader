shader_type canvas_item;

uniform vec4 modulate_color : source_color = vec4(0.0);

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	COLOR = tex;
	COLOR.rgb = mix(COLOR.rgb, modulate_color.rgb, modulate_color.a);
}
