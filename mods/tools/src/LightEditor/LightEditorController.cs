using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.UI.Lights;
using Godot;

namespace Cthangover.Core.UI.Tool.LightEditor
{
	public class LightEditorController
	{
		public List<LightDef> Lights { get; } = new();

		private readonly Vector2[] previewPositions = new Vector2[11];
		private readonly Color[] previewColors = new Color[11];
		private readonly float[] previewRadii = new float[11];
		private readonly float[] previewInfluence = new float[11];

		private bool dirty = true;

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

		public void SetBackgroundForPreview(Texture2D texture, ShaderMaterial material)
		{
			if (material != null)
				material.SetShaderParameter("background_map", texture);
		}

		public void SetDepthForPreview(Texture2D texture, ShaderMaterial material)
		{
			if (material != null)
				material.SetShaderParameter("depth_mask", texture);
		}

		public void SetAlbedoForPreview(Texture2D texture, ShaderMaterial material)
		{
			if (material != null)
				material.SetShaderParameter("albedo_map", texture);
		}

		public void AddLight(LightDef light)
		{
			Lights.Add(light);
			dirty = true;
		}

		public void RemoveLight(int index)
		{
			if (index < 0 || index >= Lights.Count)
				return;
			Lights.RemoveAt(index);
			dirty = true;
		}

		public void Clear()
		{
			Lights.Clear();
			dirty = true;
		}

		public void MarkDirty()
		{
			dirty = true;
		}

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

		public string ExportLightsJson()
		{
			var options = new JsonSerializerOptions { WriteIndented = false };
			return JsonSerializer.Serialize(Lights, options);
		}

		public string ExportScenarioSnippet()
		{
			var json = ExportLightsJson();
			return $"light_set \"{json.Replace("\"", "\\\"")}\"";
		}
	}
}
