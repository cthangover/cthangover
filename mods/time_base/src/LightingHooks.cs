using Cthangover.Core.Actions;
using Cthangover.Core.Scenes;
using Godot;

namespace Cthangover.Mods.Time
{
    /// <summary>
    /// Static adapters bound into the kernel <see cref="LightingBridge"/> on
    /// <c>OnModResourcesReady</c>. They reproduce the two lighting touch
    /// points the kernel SceneManager used to own directly: resetting the
    /// per-scene static lights and lighting maps when a scene switches, and
    /// pushing background depth/albedo textures onto the scene controller.
    /// Both degrade to no-ops when the lighting controller is absent, so the
    /// calls stay safe in scenes without a "Lights" node.
    /// </summary>
    public static class LightingHooks
    {
        /// <summary>Clears static lights and both lighting maps on the scene's controller; no-op when the scene has none.</summary>
        public static void ClearSceneLights()
        {
            var lightController = SceneContextNode.FindNode<UiLightController>("Lights");
            if (lightController == null)
                return;
            lightController.ClearStaticLights();
            lightController.SetupDepthMap(null);
            lightController.SetupAlbedoMap(null);
        }

        /// <summary>Pushes background depth/albedo textures onto the controller singleton; skipped when it is not in the tree.</summary>
        public static void SetupBackgroundMaps(Texture2D depth, Texture2D albedo)
        {
            var controller = UiLightController.Instance;
            controller?.SetupDepthMap(depth);
            controller?.SetupAlbedoMap(albedo);
        }
    }
}
