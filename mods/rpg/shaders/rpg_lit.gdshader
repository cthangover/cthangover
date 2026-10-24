shader_type canvas_item;

// Light of an RPG map (RpgLighting): the tiles and actors are multiplied by the ambient
// colour of the time of day (time_weights blends the four phases like timed_sprite), and
// point lights (the player, lit events) add to it. Lights only add up to white, so by day
// they are invisible and at night they open circles in the dark. Positions and radii are
// in canvas pixels, the space of world_pos below.

uniform vec4 day_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 morning_color : source_color = vec4(0.86, 0.72, 0.84, 1.0);
uniform vec4 evening_color : source_color = vec4(0.84, 0.72, 0.92, 1.0);
uniform vec4 night_color : source_color = vec4(0.2, 0.26, 0.42, 1.0);
uniform vec4 time_weights = vec4(0.0, 1.0, 0.0, 0.0);

// A map that sets its own ambient (indoors) uses it instead of the time of day.
uniform bool use_ambient = false;
uniform vec4 ambient_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

uniform vec2 light_positions[16];
uniform vec4 light_colors[16];
uniform float light_radii[16];
uniform int light_count = 0;

varying vec2 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec4 color = texture(TEXTURE, UV) * COLOR;

	vec3 ambient = use_ambient
		? ambient_color.rgb
		: (morning_color * time_weights.x + day_color * time_weights.y
			+ evening_color * time_weights.z + night_color * time_weights.w).rgb;

	vec3 light = vec3(0.0);
	for (int i = 0; i < 16; i++) {
		if (i >= light_count)
			break;
		float k = clamp(1.0 - distance(world_pos, light_positions[i]) / max(light_radii[i], 1.0), 0.0, 1.0);
		light += light_colors[i].rgb * light_colors[i].a * k * k;
	}

	COLOR = vec4(color.rgb * min(ambient + light, vec3(1.0)), color.a);
}
