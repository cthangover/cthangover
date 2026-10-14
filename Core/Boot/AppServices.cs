using System;
using System.Collections.Generic;
using Cthangover.Core.Factories;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Scenes;
using Cthangover.Core.Scenes.TscnBuild;
using Cthangover.Core.Scenes.TscnParsing;
using Cthangover.Core.Settings;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Menu;
using Cthangover.Core.UI.Tool;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Bootstrap
{
    /// <summary>
    /// Composition root of the game. Owns every service, registry and factory
    /// the core creates, in one place, with explicit construction order.
    ///
    /// Lifecycle: Boot creates a fresh instance at the start of every boot and
    /// publishes it through SetCurrent. Outside the running game (xUnit tests,
    /// design-time tooling) Current materializes a default instance on first
    /// touch, so facades keep working without a boot; construction of
    /// individual services stays lazy (each slot below is a Lazy of T) and is
    /// therefore timing-preserving. The default instance is throwaway: boot
    /// replaces it, so early read-only access is safe, but anything registered
    /// on it (resettables) would be discarded — runtime code registers only
    /// after the boot pipeline has begun (mod initializers run inside it).
    ///
    /// Two data-driven loops live here:
    /// - Registries: IGameRegistry.Rebuild/Clear replace hand-maintained
    ///   lists in ModManager.Reload;
    /// - Resettables: ResetForNewGame replaces scattered per-owner reset
    ///   calls and NewGameStartedEvent subscriptions; mods register extra
    ///   owners via RegisterResettable from an IModInitializer.
    ///
    /// Migration note: factory static facades (BackgroundFactory.Instance
    /// and friends) delegate into the matching slot here. The facades stay
    /// as the public API until all consumers are migrated to explicit
    /// injection. A few process-wide singletons are still owned outside this
    /// class — ModRegistry/ModManager (mod pipeline) and MapZoneService
    /// (reset through the lambda resettables below) — migrating them is a
    /// documented follow-up. Quest state moved to the quests_base engine
    /// mod, whose initializer registers its own resettable with the same
    /// "quests" id through <see cref="RegisterResettable"/>.
    /// </summary>
    public sealed class AppServices
    {
        private static AppServices current;
        private static readonly object CurrentLock = new();

        /// <summary>
        /// The active service set. In a running game this is the instance the
        /// boot pipeline published; in tests and tools it is a lazily created
        /// default. Never null after the first touch.
        /// </summary>
        public static AppServices Current
        {
            get
            {
                var snapshot = current;
                if (snapshot != null)
                    return snapshot;
                return CreateDefault();
            }
        }

        /// <summary>
        /// Publishes the boot-created service set. Called once per process by
        /// Boot before any phase runs; replaces a default instance if tests
        /// happened to materialize one earlier.
        /// </summary>
        public static void SetCurrent(AppServices instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            lock (CurrentLock)
                current = instance;
        }

        private static AppServices CreateDefault()
        {
            lock (CurrentLock)
            {
                if (current == null)
                    current = new AppServices();
                return current;
            }
        }

        /// <summary>Mod-derived registries, cleared and rebuilt as a set.</summary>
        public IReadOnlyList<IGameRegistry> Registries { get; }

        /// <summary>Typed accessors for the registries the static facades delegate to.</summary>
        public ModConflictRegistry Conflicts { get; }
        /// <summary>Mod-declared GUI entries (panels and overlays); <see cref="UiBuilder"/> reads it to build the toolbar.</summary>
        public UiRegistry Ui { get; }
        /// <summary>Menu items (buttons with their bindings) and reusable panels, from mod manifests and runtime registration.</summary>
        public MenuRegistry Menus { get; }
        /// <summary>Executable scene events discovered via attributes or registered by mods at runtime.</summary>
        public SceneEventRegistry SceneEvents { get; }
        /// <summary>Logical scene names mapped to .tscn paths, registered by mod initializers.</summary>
        public SceneRouteRegistry SceneRoutes { get; }
        /// <summary>Scene definitions collected from mod manifests, with an instance-level cache.</summary>
        public SceneDefinitions SceneDefs { get; }
        /// <summary>.tscn external-resource builders, indexed by resource type.</summary>
        public ExtResourceBuilderRegistry ExtResourceBuilders { get; }
        /// <summary>Ordered node property resolvers for .tscn building; first match wins.</summary>
        public PropertyResolverRegistry PropertyResolvers { get; }
        /// <summary>.tscn sub-resource builders, indexed by resource type.</summary>
        public SubResourceBuilderRegistry SubResourceBuilders { get; }
        /// <summary>.tscn text section parsers, indexed by section type.</summary>
        public SectionParserRegistry SectionParsers { get; }

        private readonly List<IResettable> resettables = new();
        private readonly object resettablesLock = new();

        /// <summary>Owners of playthrough state, reset together on a new game.</summary>
        public IReadOnlyList<IResettable> Resettables => resettables;

        /// <summary>
        /// Constructs the service set. Registries are created immediately because they are
        /// cheap and side-effect free by contract; core resettables (map zones) are
        /// registered here with lambda-deferred construction so the first reset keeps the
        /// pre-migration touch order; mod-owned subsystems (quests) register theirs from
        /// initializers. Factory slots stay lazy (see the slot list below).
        /// </summary>
        public AppServices()
        {
            // Registry instances are cheap and side-effect free by contract;
            // Rebuild fills them from the loaded mod set.
            Conflicts = new ModConflictRegistry();
            Ui = new UiRegistry();
            Menus = new MenuRegistry();
            SceneEvents = new SceneEventRegistry();
            SceneRoutes = new SceneRouteRegistry();
            SceneDefs = new SceneDefinitions();
            ExtResourceBuilders = new ExtResourceBuilderRegistry();
            PropertyResolvers = new PropertyResolverRegistry();
            SubResourceBuilders = new SubResourceBuilderRegistry();
            SectionParsers = new SectionParserRegistry();

            Registries = new IGameRegistry[]
            {
                Conflicts,
                Ui,
                Menus,
                SceneEvents,
                SceneRoutes,
                SceneDefs,
                ExtResourceBuilders,
                PropertyResolvers,
                SubResourceBuilders,
                SectionParsers,
            };

            // Core state owners. Lambdas keep construction deferred until the
            // first real reset, exactly like the pre-migration touch order.
            // Quest state is owned by the quests_base mod and registers its
            // own "quests" resettable from QuestsModInitializer.OnModResourcesReady.
            RegisterResettable(new LambdaResettable("map_zones", () => Cthangover.Core.Actions.MapStateBridge.ResetOverrides()));
        }

        /// <summary>
        /// Registers an additional new-game state owner (mods register theirs
        /// from an IModInitializer, i.e. after the boot pipeline has begun).
        /// Registering on Current before SetCurrent would target the throwaway
        /// default instance and be lost. Duplicates by IResettable.Id are
        /// ignored.
        /// </summary>
        public void RegisterResettable(IResettable resettable)
        {
            if (resettable == null)
                return;

            lock (resettablesLock)
            {
                foreach (var existing in resettables)
                {
                    if (string.Equals(existing.Id, resettable.Id, StringComparison.OrdinalIgnoreCase))
                        return;
                }
                resettables.Add(resettable);
            }
        }

        /// <summary>Resets every registered state owner, in registration order.</summary>
        public void ResetForNewGame()
        {
            IResettable[] snapshot;
            lock (resettablesLock)
                snapshot = resettables.ToArray();

            foreach (var resettable in snapshot)
            {
                try
                {
                    resettable.ResetForNewGame();
                    GameLogger.Log("BOOT", "reset: " + resettable.Id);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("BOOT", "reset " + resettable.Id + " FAILED: " + ex.Message, LogLevel.Error);
                }
            }
        }

        /// <summary>Clears every registry (mod reload entry point).</summary>
        public void ClearRegistries()
        {
            foreach (var registry in Registries)
            {
                try
                {
                    registry.Clear();
                    GameLogger.Log("BOOT", "cleared: " + registry.Id, LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("BOOT", "clear " + registry.Id + " FAILED: " + ex.Message, LogLevel.Error);
                }
            }
        }

        /// <summary>Rebuilds every registry from the currently loaded mods.</summary>
        public void RebuildRegistries()
        {
            foreach (var registry in Registries)
            {
                try
                {
                    registry.Rebuild();
                    GameLogger.Log("BOOT", "rebuilt: " + registry.Id, LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("BOOT", "rebuild " + registry.Id + " FAILED: " + ex.Message, LogLevel.Error);
                }
            }
        }

        // ---- Service slots ------------------------------------------------
        // Each slot is a Lazy of T so construction timing is identical to the
        // pre-migration Lazy singletons; facades on the service classes
        // delegate here (AppServices.Current.Slot.Value).

        /// <summary>Background images shown behind dialog scenes, resolved through the mod resource pipeline.</summary>
        public Lazy<BackgroundFactory> Backgrounds { get; } = new(() => new BackgroundFactory());
        /// <summary>Full-size UI panel textures (frames, overlays, menu backgrounds).</summary>
        public Lazy<UITextureFactory> UITextures { get; } = new(() => new UITextureFactory());
        /// <summary>Small UI icons (toolbar buttons, badges) from the <c>ui/icons</c> group.</summary>
        public Lazy<UIIconFactory> UIIcons { get; } = new(() => new UIIconFactory());
        /// <summary>Inventory item icons, with a dedicated LRU cache budget.</summary>
        public Lazy<ItemSpriteFactory> ItemSprites { get; } = new(() => new ItemSpriteFactory());
        /// <summary>Skill definitions for the card-based skill system (migrated to the skills_base engine mod).</summary>
        // SkillFactory lives in mods/skills_base now; the AppServices slot is gone by design.
        /// <summary>Interactive object definitions for scene hotspots (migrated to the interactive_base engine mod).</summary>
        // InteractiveFactory lives in mods/interactive_base now; the AppServices slot is gone by design.
        /// <summary>DSL scenario actions indexed by their registered <c>Name</c> (e.g. <c>"map.open_zone"</c>).</summary>
        public Lazy<ScenarioActionFactory> ScenarioActions { get; } = new(() => new ScenarioActionFactory());
        /// <summary>Dev tool windows (<see cref="IToolProvider"/> implementations), discovered by reflection.</summary>
        public Lazy<ToolFactory> Tools { get; } = new(() => new ToolFactory());
        /// <summary>Toolbar button definitions for dev tools (<see cref="IToolBoxButton"/> implementations).</summary>
        public Lazy<ToolBoxButtonFactory> ToolBoxButtons { get; } = new(() => new ToolBoxButtonFactory());
        /// <summary>Mod pipeline configuration (asset groups, assembly caching, file mappings) from <c>mod_config.json</c>.</summary>
        public Lazy<ModConfig> ModConfigService { get; } = new(() => ModConfig.CreateInstance());
        /// <summary>Game configuration (audio, display, language, logging) from <c>game_config.json</c>.</summary>
        public Lazy<GameConfig> GameConfigService { get; } = new(() => GameConfig.CreateInstance());

        /// <summary>Trivial IResettable adapter over a delegate.</summary>
        private sealed class LambdaResettable : IResettable
        {
            private readonly Action reset;

            /// <summary>Binds an id and the reset delegate it forwards to.</summary>
            public LambdaResettable(string id, Action reset)
            {
                Id = id;
                this.reset = reset;
            }

            /// <summary>Stable resettable id used for logging and duplicate detection.</summary>
            public string Id { get; }

            /// <summary>Runs the wrapped reset delegate.</summary>
            public void ResetForNewGame() => reset();
        }
    }
}
