using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

public partial class PixelArtPostProcess : Control
{
	[Export]
	public float PixelSize
	{
		get => _pixelSize;
		set
		{
			_pixelSize = value;
			UpdateShaderParameter();
		}
	}
	private float _pixelSize = 1.0f;

	private ColorRect _colorRect;
	private ShaderMaterial _shaderMaterial;

	public override void _Ready()
	{
		_colorRect = new ColorRect();
		_colorRect.Name = "PixelArtPostProcessRect";
		AddChild(_colorRect);

		var shader = ModManager.Instance.ResolveShader("pixel_art_post_process");
		if (shader == null)
		{
			GameLogger.Log("SHADER", "PixelArtPostProcess: Failed to load pixel_art_post_process.gdshader", LogLevel.Error);
			return;
		}

		_shaderMaterial = new ShaderMaterial();
		_shaderMaterial.Shader = shader;
		_colorRect.Material = _shaderMaterial;

		_colorRect.MouseFilter = MouseFilterEnum.Ignore;
		_colorRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		UpdateShaderParameter();
	}

	public override void _Process(double delta)
	{
		if (_colorRect != null)
		{
			_colorRect.Position = Vector2.Zero;
			_colorRect.Size = GetViewportRect().Size;
		}
	}

	private void UpdateShaderParameter()
	{
		if (_shaderMaterial != null)
		{
			_shaderMaterial.SetShaderParameter("pixel_size", _pixelSize);
		}
	}
}
