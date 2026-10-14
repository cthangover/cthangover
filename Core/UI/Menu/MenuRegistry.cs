using System;
using System.Collections.Generic;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Menu
{
    /// <summary>
    /// The menu registry: a single place that knows which menu items (buttons)
    /// exist, which menu blocks (sections) they belong to, and what each button
    /// is bound to — a mod scene or a code handler. Parties outside the core
    /// register their own buttons and panels into it:
    /// <list type="bullet">
    /// <item>declaratively, through the manifest <c>main_menu</c> array of any
    /// loaded mod (collected by <see cref="Rebuild"/> into
    /// <see cref="MenuButtonEntry"/>s with a scene binding);</item>
    /// <item>programmatically, from mod initializers via
    /// <see cref="RegisterButton"/> / <see cref="RegisterPanel"/>, which is how
    /// the <c>base_menu</c> mod publishes the built-in new game / load /
    /// settings / exit items and the settings and save/load panels.</item>
    /// </list>
    /// <para/>
    /// The registry is data-driven like the other IGameRegistry
    /// implementations: <see cref="Rebuild"/> re-reads the manifests of the
    /// loaded mod set, <see cref="Clear"/> returns it to the empty state and is
    /// called on mod reload (runtime registrations are dropped there too and
    /// re-added when the initializers run again). Host menus never enumerate
    /// their own buttons — they ask this registry.
    /// <para/>
    /// The instance lives in <see cref="AppServices.Current"/>; the static
    /// members are a facade delegating to it so callers keep a short API.
    /// </summary>
    public class MenuRegistry : IGameRegistry
    {
        private readonly List<MenuButtonEntry> manifestButtons = new();
        private readonly List<MenuButtonEntry> runtimeButtons = new();
        private readonly List<MenuPanelEntry> panels = new();
        private readonly Dictionary<string, MenuButtonEntry> manifestById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MenuButtonEntry> runtimeById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MenuPanelEntry> panelsById = new(StringComparer.OrdinalIgnoreCase);
        private List<MenuButtonEntry> merged = new();
        private bool built;

        /// <summary>Shared instance the static facade delegates to.</summary>
        private static MenuRegistry Owned => AppServices.Current.Menus;

        /// <summary>
        /// Creates an empty registry: cheap and side-effect free per the
        /// IGameRegistry contract. <see cref="Rebuild"/> fills the manifest part.
        /// </summary>
        internal MenuRegistry()
        {
        }

        /// <inheritdoc />
        public string Id => "menus";

        // ---- IGameRegistry -------------------------------------------------

        /// <summary>
        /// Drops the manifest-derived entries and re-collects the
        /// <c>main_menu</c> arrays of all loaded mods. Runtime registrations are
        /// kept and remain part of the combined view. Safe to call repeatedly.
        /// </summary>
        public void Rebuild()
        {
            manifestButtons.Clear();
            manifestById.Clear();
            built = true;
            CollectManifest();
            RebuildMerged();
            GameLogger.Log("MENU_REGISTRY", $"Collect → {manifestButtons.Count} manifest button(s), {runtimeButtons.Count} runtime button(s), {panels.Count} panel(s)");
        }

        /// <summary>
        /// Drops everything, including runtime registrations, so the next build
        /// re-reads the manifests. Called on mod reload through
        /// AppServices.ClearRegistries; the reload flow re-runs the mod
        /// initializers right after, which re-register their items.
        /// </summary>
        void IGameRegistry.Clear()
        {
            manifestButtons.Clear();
            runtimeButtons.Clear();
            panels.Clear();
            manifestById.Clear();
            runtimeById.Clear();
            panelsById.Clear();
            merged = new List<MenuButtonEntry>();
            built = false;
        }

        /// <summary>Collects the manifest part unless already built.</summary>
        public void EnsureBuilt()
        {
            if (!built)
                Rebuild();
        }

        // ---- Runtime registration API --------------------------------------

        /// <summary>
        /// Registers a button from mod code. The entry must have an id, a
        /// locale and a binding (scene path or handler); duplicates and invalid
        /// entries are logged and skipped, the registry state stays consistent.
        /// </summary>
        public void RegisterButton(MenuButtonEntry entry)
        {
            if (entry == null)
                return;

            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                GameLogger.Log("MENU_REGISTRY", "RegisterButton: missing 'id'", LogLevel.Error);
                return;
            }

            if (runtimeById.ContainsKey(entry.Id) || manifestById.ContainsKey(entry.Id))
            {
                GameLogger.Log("MENU_REGISTRY", $"RegisterButton '{entry.Id}': duplicate id, keeping the first registration", LogLevel.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(entry.Locale))
            {
                GameLogger.Log("MENU_REGISTRY", $"RegisterButton '{entry.Id}': missing 'locale'", LogLevel.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(entry.ScenePath) && entry.Handler == null)
            {
                GameLogger.Log("MENU_REGISTRY", $"RegisterButton '{entry.Id}': needs a scene_path or a handler", LogLevel.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(entry.Section))
                entry.Section = MenuButtonEntry.SectionMods;

            entry.ModOrderIndex = -1;
            runtimeButtons.Add(entry);
            runtimeById[entry.Id] = entry;
            RebuildMerged();
            GameLogger.Log("MENU_REGISTRY", $"RegisterButton → '{entry.Id}' (section '{entry.Section}', index {entry.Index})");
        }

        /// <summary>
        /// Registers a reusable overlay panel from mod code. Panels are opened
        /// by id through <see cref="InstantiatePanel"/>; duplicates and entries
        /// without a scene path are logged and skipped.
        /// </summary>
        public void RegisterPanel(MenuPanelEntry entry)
        {
            if (entry == null)
                return;

            if (entry.IsValid == false || string.IsNullOrWhiteSpace(entry.ScenePath))
            {
                GameLogger.Log("MENU_REGISTRY", "RegisterPanel: entry requires 'id' and 'scene_path'", LogLevel.Error);
                return;
            }

            if (panelsById.ContainsKey(entry.Id))
            {
                GameLogger.Log("MENU_REGISTRY", $"RegisterPanel '{entry.Id}': duplicate id, keeping the first registration", LogLevel.Error);
                return;
            }

            panels.Add(entry);
            panelsById[entry.Id] = entry;
            GameLogger.Log("MENU_REGISTRY", $"RegisterPanel → '{entry.Id}' ({entry.ScenePath})");
        }

        // ---- Query API -----------------------------------------------------

        /// <summary>All buttons — manifest and runtime — sorted by section order (index, then mod order, then id).</summary>
        public IReadOnlyList<MenuButtonEntry> Buttons => merged;

        /// <summary>All registered panels in registration order.</summary>
        public IReadOnlyList<MenuPanelEntry> Panels => panels;

        /// <summary>Returns the button with the given id, or null when unknown.</summary>
        public MenuButtonEntry GetButton(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;
            return runtimeById.TryGetValue(id, out var runtime) ? runtime
                : manifestById.TryGetValue(id, out var manifest) ? manifest
                : null;
        }

        /// <summary>Returns the panel with the given id, or null when unknown.</summary>
        public MenuPanelEntry GetPanel(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;
            return panelsById.TryGetValue(id, out var entry) ? entry : null;
        }

        /// <summary>
        /// Instantiates the scene of the panel with the given id through the
        /// runtime tscn pipeline, or returns null when the id is unknown or the
        /// scene cannot be built. The caller owns the node (positioning,
        /// visibility, removal).
        /// </summary>
        public Node InstantiatePanel(string id)
        {
            var entry = GetPanel(id);
            if (entry == null)
            {
                GameLogger.Log("MENU_REGISTRY", $"InstantiatePanel '{id}': panel not registered", LogLevel.Error);
                return null;
            }

            var node = new ModScenePrefab(entry.ScenePath).Instantiate();
            if (node == null)
            {
                GameLogger.Log("MENU_REGISTRY", $"InstantiatePanel '{id}': scene '{entry.ScenePath}' failed to build", LogLevel.Error);
                return null;
            }

            GameLogger.Log("MENU_REGISTRY", $"InstantiatePanel '{id}' → {entry.ScenePath}");
            return node;
        }

        // ---- Implementation ------------------------------------------------

        private void CollectManifest()
        {
            var manager = ModManager.Instance;
            if (manager?.Mods == null)
            {
                GameLogger.Log("MENU_REGISTRY", "Collect: mods not loaded", LogLevel.Error);
                return;
            }

            var orderedIds = ModRegistry.Instance.GetOrderedModIds();
            for (var order = 0; order < orderedIds.Count; order++)
            {
                var modId = orderedIds[order];
                if (!manager.Mods.TryGetValue(modId, out var info) || info is not ModInfo modInfo)
                    continue;

                var declarations = modInfo.Manifest?.MainMenu;
                if (declarations == null || declarations.Count == 0)
                    continue;

                foreach (var raw in declarations)
                {
                    if (raw == null)
                        continue;

                    var entry = new MenuButtonEntry
                    {
                        Id = raw.Id,
                        ModId = modId,
                        Section = MenuButtonEntry.SectionMods,
                        Locale = raw.Button?.Locale,
                        Icon = raw.Button?.Icon,
                        Index = raw.Button?.Index ?? 10,
                        ScenePath = raw.ScenePath,
                        ModOrderIndex = order,
                    };

                    if (string.IsNullOrWhiteSpace(entry.Id))
                    {
                        GameLogger.Log("MENU_REGISTRY", $"invalid main_menu entry in mod '{modId}': missing 'id'", LogLevel.Error);
                        continue;
                    }

                    if (manifestById.ContainsKey(entry.Id) || runtimeById.ContainsKey(entry.Id))
                    {
                        GameLogger.Log("MENU_REGISTRY", $"invalid main_menu entry '{modId}/{entry.Id}': duplicate id", LogLevel.Error);
                        continue;
                    }

                    if (!entry.IsValid)
                    {
                        GameLogger.Log("MENU_REGISTRY", $"invalid main_menu entry '{modId}/{entry.Id}': 'button.locale' is required", LogLevel.Error);
                        continue;
                    }

                    var provider = modInfo.FileProvider;
                    if (provider != null && !provider.FileExists(entry.ScenePath))
                        GameLogger.Log("MENU_REGISTRY", $"main_menu entry '{modId}/{entry.Id}': scene_path '{entry.ScenePath}' not found in mod '{modId}'", LogLevel.Warning);

                    manifestButtons.Add(entry);
                    manifestById[entry.Id] = entry;
                }
            }
        }

        private void RebuildMerged()
        {
            merged = new List<MenuButtonEntry>(manifestButtons.Count + runtimeButtons.Count);
            merged.AddRange(manifestButtons);
            merged.AddRange(runtimeButtons);
            merged.Sort(CompareButtons);
        }

        private static int CompareButtons(MenuButtonEntry a, MenuButtonEntry b)
        {
            var index = a.Index.CompareTo(b.Index);
            if (index != 0)
                return index;
            var modOrder = a.ModOrderIndex.CompareTo(b.ModOrderIndex);
            if (modOrder != 0)
                return modOrder;
            return string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
        }

        // ---- Static facade -------------------------------------------------

        /// <summary>All buttons in the AppServices-owned registry, sorted.</summary>
        public static IReadOnlyList<MenuButtonEntry> AllButtons => Owned.Buttons;

        /// <summary>All panels in the AppServices-owned registry.</summary>
        public static IReadOnlyList<MenuPanelEntry> AllPanels => Owned.Panels;

        /// <summary>Registers a button in the AppServices-owned registry.</summary>
        public static void RegisterMenuButton(MenuButtonEntry entry) => Owned.RegisterButton(entry);

        /// <summary>Registers a panel in the AppServices-owned registry.</summary>
        public static void RegisterMenuPanel(MenuPanelEntry entry) => Owned.RegisterPanel(entry);

        /// <summary>Returns the button with the given id from the AppServices-owned registry.</summary>
        public static MenuButtonEntry GetButtonById(string id) => Owned.GetButton(id);

        /// <summary>Returns the panel with the given id from the AppServices-owned registry.</summary>
        public static MenuPanelEntry GetPanelById(string id) => Owned.GetPanel(id);

        /// <summary>Instantiates the panel scene by id in the AppServices-owned registry.</summary>
        public static Node OpenPanel(string id) => Owned.InstantiatePanel(id);
    }
}
