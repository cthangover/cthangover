using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.UI.Lights;
using Godot;

namespace Cthangover.Core.UI.Tool.LightEditor
{
    /// <summary>
    /// Data controller for the light editor tool. Maintains a list of
    /// <see cref="LightDef"/> objects and feeds up to 10 lights plus one
    /// sentinel entry into the <c>"timed_sprite"</c> shader material for
    /// real-time preview. Exports lights as JSON (<see cref="ExportLightsJson"/>)
    /// or as a scenario DSL snippet (<see cref="ExportScenarioSnippet"/>).
    /// Uses a dirty-flag pattern so shader uniform updates only occur when
    /// the light list changes.
    /// </summary>
	public class LightEditorController
	{
        /// <summary>The editable list of light definitions. Capped at 10 for shader preview.</summary>
		public List<LightDef> Lights { get; } = new();

		private readonly Vector2[] previewPositions = new Vector2[11];
		private readonly Color[] previewColors = new Color[11];
		private readonly float[] previewRadii = new float[11];
		private readonly float[] previewInfluence = new float[11];

		private bool dirty = true;

        /// <summary>Initialises preview arrays with out-of-bounds sentinel values.</summary>
        public LightEditorController()
		{
			for (int i = 0; i < 11; i++)
			{
				previewPositions[i] = new Vector2(-1000f, -1000f);
				previewRadii[i] = 0f;
				previewInfluence[i] = 0f;
				previewColors[i] = Colors.White;
			}
		}

        /// <summary>
        /// Creates a <see cref="ShaderMaterial"/> using the <c>"timed_sprite"</c> shader
        /// with night-time weights applied. Returns <c>null</c> if the shader is not found.
        /// </summary>
        public ShaderMaterial CreatePreviewMaterial()
		{
			var shader = ModManager.Instance.ResolveShader("timed_sprite");
			if (shader == null)
				return null;

			var mat = new ShaderMaterial { Shader = shader };
			var night = new Vector4(0, 0, 0, 1);
			mat.SetShaderParameter("time_weights", night);
			mat.SetShaderParameter("use_time", true);
			return mat;
		}

        /// <summary>Sets the background texture uniform on the preview shader material.</summary>
        public void SetBackgroundForPreview(Texture2D texture, ShaderMaterial material)
		{
			if (material != null)
				material.SetShaderParameter("background_map", texture);
		}

        /// <summary>Sets the depth mask texture uniform on the preview shader material.</summary>
		public void SetDepthForPreview(Texture2D texture, ShaderMaterial material)
		{
			if (material != null)
				material.SetShaderParameter("depth_mask", texture);
		}

        /// <summary>Sets the albedo texture uniform on the preview shader material.</summary>
		public void SetAlbedoForPreview(Texture2D texture, ShaderMaterial material)
		{
			if (material != null)
				material.SetShaderParameter("albedo_map", texture);
		}

        /// <summary>Appends a <see cref="LightDef"/> to the list and marks the preview dirty.</summary>
        public void AddLight(LightDef light)
		{
			Lights.Add(light);
			dirty = true;
		}

        /// <summary>Removes the light at <paramref name="index"/>. No-op if out of range.</summary>
        public void RemoveLight(int index)
		{
			if (index < 0 || index >= Lights.Count)
				return;
			Lights.RemoveAt(index);
			dirty = true;
		}

        /// <summary>Removes all lights from the list and marks dirty.</summary>
        public void Clear()
		{
			Lights.Clear();
			dirty = true;
		}

        /// <summary>Forces the preview to be recalculated on the next <see cref="UpdatePreview"/> call.</summary>
        public void MarkDirty()
		{
			dirty = true;
		}

        /// <summary>
        /// Packs up to 10 lights plus a sentinel into shader uniform arrays.
        /// Only executes if <c>dirty</c> is <c>true</c>. Converts each light's
        /// normalised position to pixel coordinates relative to the preview area.
        /// </summary>
        public void UpdatePreview(ShaderMaterial material, Vector2 previewGlobalPos, Vector2 previewSize)
		{
			if (material == null || !dirty)
				return;

			for (int i = 0; i < 11; i++)
			{
				previewPositions[i] = new Vector2(-1000f, -1000f);
				previewRadii[i] = 0f;
				previewInfluence[i] = 0f;
				previewColors[i] = Colors.White;
			}

			int count = Mathf.Min(Lights.Count, 10);
			for (int i = 0; i < count; i++)
			{
				var pixelPos = Lights[i].ToPixelPos(previewSize);
				previewPositions[i] = new Vector2(
					pixelPos.X + previewGlobalPos.X,
					pixelPos.Y + previewGlobalPos.Y);
				previewRadii[i] = Lights[i].Radius;
				previewInfluence[i] = Lights[i].Influence;
				previewColors[i] = Lights[i].ToColor();
			}

			var posArray = new Godot.Collections.Array<Vector2>();
			var colArray = new Godot.Collections.Array<Color>();
			var radArray = new Godot.Collections.Array<float>();
			var infArray = new Godot.Collections.Array<float>();
			for (int i = 0; i < 11; i++)
			{
				posArray.Add(previewPositions[i]);
				colArray.Add(previewColors[i]);
				radArray.Add(previewRadii[i]);
				infArray.Add(previewInfluence[i]);
			}

			material.SetShaderParameter("light_count", count);
			material.SetShaderParameter("light_positions", posArray);
			material.SetShaderParameter("light_colors", colArray);
			material.SetShaderParameter("light_radii", radArray);
			material.SetShaderParameter("light_influence", infArray);
			material.SetShaderParameter("light_radius_scale", 1.0f);

			dirty = false;
		}

        /// <summary>Serialises the light list as a compact JSON string.</summary>
        public string ExportLightsJson()
		{
			var options = new JsonSerializerOptions { WriteIndented = false };
			return JsonSerializer.Serialize(Lights, options);
		}

        /// <summary>Produces a <c>light_set</c> scenario DSL command with the lights JSON as its argument.</summary>
        public string ExportScenarioSnippet()
		{
			var json = ExportLightsJson();
			return $"light_set \"{json.Replace("\"", "\\\"")}\"";
		}
	}
}
