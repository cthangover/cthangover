using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

/// <summary>
/// Runtime shader and texture override system for modded scenes.
/// Walks the entire node tree of a freshly-instantiated scene,
/// matching each <c>ShaderMaterial</c> by its resource-path name
/// against the mod shader registry and each <c>TextureRect</c> /
/// <c>Sprite2D</c> texture by name against the mod texture registry.
///
/// Replacement is name-based (matching <c>Path.GetFileNameWithoutExtension</c>
/// of the original resource path to a mod file entry), so a mod providing
/// <c>shaders/outline.gdshader</c> will automatically override every node
/// whose material originally referenced <c>res://shaders/outline.gdshader</c>.
///
/// Called from <c>SceneLoader</c> immediately after <c>Instantiate</c>,
/// before <c>_Ready</c> fires on any child — thus overridden materials
/// are in place before any node runs its init logic.
/// </summary>
public static class ShaderModReplacer
{
    /// <summary>
    /// Entry point called after scene instantiation. Runs both the
    /// shader-material pass and the texture pass in one tree walk,
    /// logging counts of each replacement type.
    /// </summary>
    public static void PatchScene(Node root)
    {
        var shaderReplaced = PatchShaderMaterials(root);
        var textureReplaced = PatchTextures(root);

        if (shaderReplaced > 0)
            GameLogger.Log("SHADER", $"ShaderModReplacer patched {shaderReplaced} shader material(s)");

        if (textureReplaced > 0)
            GameLogger.Log("SHADER", $"ShaderModReplacer patched {textureReplaced} texture(s)");
    }

    private static int PatchShaderMaterials(Node root)
    {
        var shaders = ModManager.Instance.CollectShaders();
        if (shaders.Count == 0)
            return 0;

        var replaced = 0;
        PatchShaderNode(root, shaders, ref replaced);
        return replaced;
    }

    private static void PatchShaderNode(Node node, System.Collections.Generic.Dictionary<string, FileEntry> shaders, ref int replaced)
    {
        if (node is CanvasItem canvasItem && canvasItem.Material is ShaderMaterial mat)
        {
            var shader = mat.Shader;
            if (shader != null && !string.IsNullOrEmpty(shader.ResourcePath))
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(shader.ResourcePath);
                if (shaders.TryGetValue(name, out var entry))
                {
                    var overrideShader = ModManager.Instance.TryGetCachedShader(name)
                        ?? ModManager.Instance.ResolveShader(name);
                    if (overrideShader != null && overrideShader != shader)
                    {
                        mat.Shader = overrideShader;
                        replaced++;

                        GameLogger.Log("SHADER", $"  replaced shader '{name}' → mod '{entry.ModId}'");
                    }
                }
            }
        }

        foreach (var child in node.GetChildren())
            PatchShaderNode(child, shaders, ref replaced);
    }

    private static int PatchTextures(Node root)
    {
        var textures = ModManager.Instance.CollectTextures();
        if (textures.Count == 0)
            return 0;

        var replaced = 0;
        PatchTextureNode(root, textures, ref replaced);
        return replaced;
    }

    private static void PatchTextureNode(Node node, System.Collections.Generic.Dictionary<string, FileEntry> textures, ref int replaced)
    {
        Texture2D currentTexture = null;

        if (node is TextureRect textureRect)
            currentTexture = textureRect.Texture;
        else if (node is Sprite2D sprite)
            currentTexture = sprite.Texture;

        if (currentTexture != null && !string.IsNullOrEmpty(currentTexture.ResourcePath))
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(currentTexture.ResourcePath);
            if (textures.TryGetValue(name, out var entry))
            {
                var overrideTexture = ModManager.Instance.TryGetCachedTexture(name)
                    ?? ModManager.Instance.ResolveTexture(name);
                if (overrideTexture != null && overrideTexture != currentTexture)
                {
                    if (node is TextureRect tr)
                        tr.Texture = overrideTexture;
                    else if (node is Sprite2D sp)
                        sp.Texture = overrideTexture;

                    replaced++;
                    GameLogger.Log("SHADER", $"  replaced texture '{name}' → mod '{entry.ModId}'");
                }
            }
        }

        foreach (var child in node.GetChildren())
            PatchTextureNode(child, textures, ref replaced);
    }
}
