using System.Collections.Generic;
using System.Globalization;
using Cthangover.Core.Localization;
using Cthangover.Mods.Dialog;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Scenarios
{
    /// <summary>
    /// Handles the <c>switch_scene</c> DSL command. Transitions to a new Godot
    /// scene (PackedScene) with configurable transition <c>speed</c> and an
    /// optional <c>hidden</c> mode that shows only the scene background during
    /// the transition. Implements <see cref="ICommandReferenceMetadata"/> with
    /// <see cref="PositionalReferenceKind.Scene"/> for build-time dependency tracking.
    /// </summary>
    public class SwitchSceneCommandStrategy : IScenarioCommandStrategy, ICommandReferenceMetadata, IScenarioCommandSpec
    {
        /// <inheritdoc/>
        public string Command => "switch_scene";
        /// <inheritdoc/>
        public PositionalReferenceKind Positional0Kind => PositionalReferenceKind.Scene;
        /// <inheritdoc/>
        public int MinPositional => 1;
        /// <inheritdoc/>
        public int MaxPositional => 1;
        /// <inheritdoc/>
        public IReadOnlyList<string> AllowedNamed => new[] { "speed", "hidden" };

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            var scene = positional.Count > 0 ? positional[0] : "";
            var speed = named.TryGetValue("speed", out var sp) ? ParseFloat(sp) : 4f;
            var hidden = named.TryGetValue("hidden", out var h) && h == "true";
            dlg.SwitchScene(scene, speed, hidden);
        }

        private static float ParseFloat(string s)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
        }
    }
}
