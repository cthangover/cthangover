namespace Cthangover.Core.Mods.HotReload.Impls
{
    using System.Text.Json.Nodes;
    using Cthangover.Core.Settings;
    using Godot;

    /// <summary>Detached render values. Nullable fields also let a command identify the settings it will touch.</summary>
    internal readonly record struct DevelopmentDisplayValues(int? MaxFps, DisplayServer.VSyncMode? VsyncMode, float? RenderScale3D)
    {
        internal static DevelopmentDisplayValues Capture(Viewport viewport)
        {
            DisplayServer.VSyncMode? vsync = DisplayServer.GetName() == "headless" ? null : DisplayServer.WindowGetVsyncMode();
            return new DevelopmentDisplayValues(Engine.MaxFps, vsync, viewport.Scaling3DScale);
        }

        internal static DevelopmentDisplayValues ReadProfile(SettingsData settings)
        {
            return new DevelopmentDisplayValues(settings.MaxFps,
                settings.VsyncEnabled ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled,
                settings.RenderScale3D);
        }

        internal DevelopmentDisplayValues Select(DevelopmentDisplayValues mask)
        {
            return new DevelopmentDisplayValues(mask.MaxFps.HasValue ? MaxFps : null,
                mask.VsyncMode.HasValue ? VsyncMode : null, mask.RenderScale3D.HasValue ? RenderScale3D : null);
        }

        internal void Apply(Viewport viewport)
        {
            DisplaySettingsService.ApplyRendering(MaxFps, VsyncMode, viewport, RenderScale3D);
        }

        internal void UpdateProfile(SettingsData profile, DevelopmentDisplayValues mask)
        {
            if (mask.MaxFps.HasValue)
                profile.MaxFps = MaxFps.Value;
            if (mask.VsyncMode.HasValue)
                profile.VsyncEnabled = VsyncMode != DisplayServer.VSyncMode.Disabled;
            if (mask.RenderScale3D.HasValue)
                profile.RenderScale3D = RenderScale3D;
        }

        internal JsonObject ToJson() => new()
        {
            ["max_fps"] = MaxFps,
            ["vsync_enabled"] = VsyncMode.HasValue ? JsonValue.Create(VsyncMode != DisplayServer.VSyncMode.Disabled) : null,
            ["vsync_mode"] = VsyncMode.HasValue ? VsyncMode.Value.ToString().ToLowerInvariant() : null,
            ["render_scale_3d"] = RenderScale3D
        };
    }
}
