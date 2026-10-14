using System;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI
{
    /// <summary>
    /// Core-side bridge for UI theming. The implementation (CthangoverTheme,
    /// interface engine mod) installs its hook through <see cref="Register"/>
    /// when the mod initializer runs; the kernel call sites (UiHost's shell
    /// and panel builds) only know this delegate slot. Registration is
    /// first-wins: reloading the mod re-runs its static initializers, and a
    /// second hook must never stack on top of the first. While no hooks are
    /// bound (kernel built without the interface mod, or calls before mods
    /// load) invocations are skipped with a warning instead of throwing.
    /// Same pattern as <c>LightingBridge</c> for the time mod.
    /// </summary>
    public static class UiThemeBridge
    {
        private static readonly object Gate = new();
        private static Action<Node> _applyTheme;

        /// <summary>Installs the theme hook; first registration wins, re-registrations are logged and ignored.</summary>
        public static void Register(Action<Node> applyTheme)
        {
            if (applyTheme == null)
                return;

            lock (Gate)
            {
                if (_applyTheme != null)
                {
                    GameLogger.Log("UI", "UiThemeBridge: theme hook already registered; ignoring re-registration", LogLevel.Warning);
                    return;
                }

                _applyTheme = applyTheme;
            }
        }

        /// <summary>
        /// Applies the mod theme to a freshly built control or shell root.
        /// Skipped with a warning when no interface implementation is bound.
        /// </summary>
        public static void Apply(Node root)
        {
            if (root == null)
                return;

            Action<Node> hook;
            lock (Gate)
                hook = _applyTheme;

            if (hook == null)
            {
                GameLogger.Log("UI", "UiThemeBridge: no theme hook bound (interface mod not loaded); theme application skipped", LogLevel.Warning);
                return;
            }

            hook(root);
        }
    }
}
