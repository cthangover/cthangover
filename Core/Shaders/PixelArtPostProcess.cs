using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

/// <summary>
/// Full-screen post-processing overlay that applies a pixel-art
/// downscale effect via a mod-provided <c>pixel_art_post_process.gdshader</c>.
///
/// On <c>_Ready</c>, creates a <c>ColorRect</c> covering the full viewport
/// (mouse passthrough, <c>FullRect</c> anchors) and loads the shader
/// through <c>ModManager.ResolveShader</c>. The shader receives a single
/// uniform — <c>pixel_size</c> — via the <see cref="PixelSize"/> property,
/// which can be adjusted at runtime through the Godot inspector.
///
/// This node is typically added as an <c>AddChild</c> to the scene root
/// during scene construction from a mod subscription or factory.
/// </summary>
public partial class PixelArtPostProcess : Control
{
    /// <summary>
    /// The pixel-art block size passed to the shader as the
    /// <c>pixel_size</c> uniform. Setting this at runtime immediately
    /// updates the shader via <c>SetShaderParameter</c>.
    /// </summary>
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

    /// <summary>
    /// Creates the full-screen <c>ColorRect</c>, loads the
    /// <c>pixel_art_post_process</c> shader from the mod system,
    /// and sets the initial <c>pixel_size</c> uniform value.
    /// </summary>
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

    /// <summary>
    /// Ensures the overlay <c>ColorRect</c> always covers the entire
    /// viewport, even after window resize or resolution changes.
    /// </summary>
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
