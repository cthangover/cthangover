using System;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.UI.Menu;
using Godot;

namespace Engine.Tests.Menu
{
    /// <summary>
    /// Unit tests for <see cref="MenuRegistry"/>: runtime button/panel
    /// registration, validation and duplicate skipping, ordering, lookup and
    /// the Rebuild/Clear lifecycle. Every test publishes a fresh AppServices
    /// instance (Save → SetCurrent → restore) and clears the registry it
    /// touched, so no state leaks into other tests through the static facade.
    /// <para/>
    /// Manifest collection is not exercised here: without a loaded
    /// ModManager, Rebuild collects an empty manifest part, which the
    /// lifecycle tests rely on directly.
    /// </summary>
    [Collection("appservices")]
    public class MenuRegistryTests
    {
        private static MenuButtonEntry Button(
            string id,
            int index = 10,
            string section = null,
            string locale = "ui/test",
            string scenePath = "scenes/test/button.tscn") =>
            new MenuButtonEntry
            {
                Id = id,
                ModId = "test_mod",
                Section = section,
                Locale = locale,
                Index = index,
                ScenePath = scenePath,
            };

        private static MenuPanelEntry Panel(string id, string scenePath = "scenes/test/panel.tscn") =>
            new MenuPanelEntry { Id = id, ModId = "test_mod", ScenePath = scenePath };

        // ---- IGameRegistry contract ----------------------------------------

        [Fact]
        public void RegistryId_IsMenus()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                Assert.Equal("menus", ((IGameRegistry)services.Menus).Id);
            }
            finally
            {
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void FreshRegistry_IsEmptyAndRebuildIsIdempotent()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                var menus = services.Menus;

                // Cheap ctor: nothing collected, nothing registered.
                Assert.Empty(menus.Buttons);
                Assert.Empty(menus.Panels);

                menus.Rebuild();
                menus.Rebuild();

                // No mods are loaded in unit tests, so the manifest part
                // stays empty and Rebuild is safe to repeat.
                Assert.Empty(menus.Buttons);
                Assert.Empty(menus.Panels);
            }
            finally
            {
                ((IGameRegistry)services.Menus).Clear();
                AppServices.SetCurrent(saved);
            }
        }

        // ---- RegisterButton -------------------------------------------------

        [Fact]
        public void RegisterButton_ValidEntry_IsListedAndFindable()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                var entry = Button("test_mod/start");
                services.Menus.RegisterButton(entry);

                Assert.Single(services.Menus.Buttons);
                Assert.Same(entry, services.Menus.Buttons[0]);

                var found = services.Menus.GetButton("test_mod/start");
                Assert.NotNull(found);
                Assert.Same(entry, found);

                // Lookup is case-insensitive like the other registries.
                Assert.Same(entry, services.Menus.GetButton("TEST_MOD/START"));

                Assert.Null(services.Menus.GetButton("missing"));
                Assert.Null(services.Menus.GetButton(null));
                Assert.Null(services.Menus.GetButton(""));
            }
            finally
            {
                ((IGameRegistry)services.Menus).Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void RegisterButton_DefaultsSectionAndModOrder()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                // Section null → the mod block; runtime entries keep order -1
                // so equal-index registrations sort before manifest entries.
                var entry = Button("test_mod/no_section", section: null);
                services.Menus.RegisterButton(entry);

                Assert.Equal(MenuButtonEntry.SectionMods, entry.Section);
                Assert.Equal(-1, entry.ModOrderIndex);
            }
            finally
            {
                ((IGameRegistry)services.Menus).Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void RegisterButton_HandlerOnlyEntry_IsValid()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                Action<Control> handler = control => { };
                var entry = new MenuButtonEntry
                {
                    Id = "test_mod/exit",
                    Locale = "ui/test/exit",
                    Handler = handler,
                };
                services.Menus.RegisterButton(entry);

                Assert.Same(handler, services.Menus.GetButton("test_mod/exit")?.Handler);
                Assert.Null(services.Menus.GetButton("test_mod/exit").ScenePath);
            }
            finally
            {
                ((IGameRegistry)services.Menus).Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void RegisterButton_InvalidEntries_AreSkipped()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                var menus = services.Menus;

                menus.RegisterButton(null);
                menus.RegisterButton(Button(null));
                menus.RegisterButton(Button("   "));
                menus.RegisterButton(Button("no_locale", locale: null));
                menus.RegisterButton(Button("no_locale_empty", locale: ""));
                // Neither a scene nor a handler.
                menus.RegisterButton(Button("no_binding", scenePath: null));

                Assert.Empty(menus.Buttons);
                Assert.Null(menus.GetButton("no_locale"));
                Assert.Null(menus.GetButton("no_binding"));
            }
            finally
            {
                ((IGameRegistry)services.Menus).Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void RegisterButton_DuplicateId_KeepsFirstRegistration()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                var first = Button("test_mod/one", scenePath: "scenes/test/first.tscn");
                var second = Button("test_mod/one", scenePath: "scenes/test/second.tscn");
                services.Menus.RegisterButton(first);
                services.Menus.RegisterButton(second);

                Assert.Single(services.Menus.Buttons);
                Assert.Same(first, services.Menus.GetButton("test_mod/one"));
                Assert.Equal("scenes/test/first.tscn", services.Menus.GetButton("test_mod/one").ScenePath);
            }
            finally
            {
                ((IGameRegistry)services.Menus).Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void Buttons_AreSortedByIndexThenId()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                var menus = services.Menus;
                menus.RegisterButton(Button("test_mod/middle", index: 20));
                menus.RegisterButton(Button("test_mod/first", index: 10));
                menus.RegisterButton(Button("test_mod/last", index: 100));
                // Equal index breaks the tie on id, case-insensitively.
                menus.RegisterButton(Button("test_mod/tie_b", index: 20));
                menus.RegisterButton(Button("test_mod/tie_a", index: 20));

                Assert.Equal(
                    new[] { "test_mod/first", "test_mod/middle", "test_mod/tie_a", "test_mod/tie_b", "test_mod/last" },
                    System.Linq.Enumerable.Select(menus.Buttons, b => b.Id));
            }
            finally
            {
                ((IGameRegistry)services.Menus).Clear();
                AppServices.SetCurrent(saved);
            }
        }

        // ---- RegisterPanel --------------------------------------------------

        [Fact]
        public void RegisterPanel_ValidEntry_IsListedAndFindable()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                var entry = Panel("test_mod/settings");
                services.Menus.RegisterPanel(entry);

                Assert.Single(services.Menus.Panels);
                Assert.Same(entry, services.Menus.GetPanel("test_mod/settings"));
                Assert.Same(entry, services.Menus.GetPanel("TEST_MOD/SETTINGS"));

                Assert.Null(services.Menus.GetPanel("missing"));
                Assert.Null(services.Menus.GetPanel(null));
                Assert.Null(services.Menus.GetPanel(""));
            }
            finally
            {
                ((IGameRegistry)services.Menus).Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void RegisterPanel_InvalidOrDuplicateEntries_AreSkipped()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                var menus = services.Menus;

                menus.RegisterPanel(null);
                menus.RegisterPanel(Panel(null));
                menus.RegisterPanel(Panel("no_scene", scenePath: null));
                menus.RegisterPanel(Panel("blank_scene", scenePath: " "));

                var first = Panel("test_mod/one", scenePath: "scenes/test/first.tscn");
                menus.RegisterPanel(first);
                menus.RegisterPanel(Panel("test_mod/one", scenePath: "scenes/test/second.tscn"));

                Assert.Single(menus.Panels);
                Assert.Equal("scenes/test/first.tscn", menus.GetPanel("test_mod/one").ScenePath);
            }
            finally
            {
                ((IGameRegistry)services.Menus).Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void InstantiatePanel_UnknownId_ReturnsNull()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                Assert.Null(services.Menus.InstantiatePanel("missing"));
                Assert.Null(services.Menus.InstantiatePanel(null));
            }
            finally
            {
                ((IGameRegistry)services.Menus).Clear();
                AppServices.SetCurrent(saved);
            }
        }

        // ---- Lifecycle ------------------------------------------------------

        [Fact]
        public void Rebuild_KeepsRuntimeRegistrations()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                var menus = services.Menus;
                var button = Button("test_mod/persistent");
                var panel = Panel("test_mod/panel");
                menus.RegisterButton(button);
                menus.RegisterPanel(panel);

                menus.Rebuild();

                // Rebuild re-reads only the manifest part; runtime entries
                // registered by mod initializers survive it.
                Assert.Same(button, menus.GetButton("test_mod/persistent"));
                Assert.Same(panel, menus.GetPanel("test_mod/panel"));
                Assert.Single(menus.Buttons);
                Assert.Single(menus.Panels);
            }
            finally
            {
                ((IGameRegistry)services.Menus).Clear();
                AppServices.SetCurrent(saved);
            }
        }

        [Fact]
        public void Clear_DropsRuntimeRegistrationsToo()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                var menus = services.Menus;
                menus.RegisterButton(Button("test_mod/button"));
                menus.RegisterPanel(Panel("test_mod/panel"));

                ((IGameRegistry)menus).Clear();

                Assert.Empty(menus.Buttons);
                Assert.Empty(menus.Panels);
                Assert.Null(menus.GetButton("test_mod/button"));
                Assert.Null(menus.GetPanel("test_mod/panel"));

                // Clear returns the registry to the just-constructed state:
                // it builds again cleanly afterwards.
                menus.EnsureBuilt();
                menus.RegisterButton(Button("test_mod/button2"));
                Assert.Single(menus.Buttons);
            }
            finally
            {
                ((IGameRegistry)services.Menus).Clear();
                AppServices.SetCurrent(saved);
            }
        }

        // ---- Static facade --------------------------------------------------

        [Fact]
        public void Facade_WorksExactlyOverAppServicesCurrentMenus()
        {
            var saved = AppServices.Current;
            var services = new AppServices();
            AppServices.SetCurrent(services);
            try
            {
                var button = Button("test_mod/facade_button");
                var panel = Panel("test_mod/facade_panel");

                // Registration through the static facade lands in the
                // AppServices-owned registry...
                MenuRegistry.RegisterMenuButton(button);
                MenuRegistry.RegisterMenuPanel(panel);

                Assert.Same(button, services.Menus.GetButton("test_mod/facade_button"));
                Assert.Same(panel, services.Menus.GetPanel("test_mod/facade_panel"));
                Assert.Same(button, MenuRegistry.GetButtonById("test_mod/facade_button"));
                Assert.Contains(MenuRegistry.AllButtons, b => b.Id == "test_mod/facade_button");
                Assert.Contains(MenuRegistry.AllPanels, p => p.Id == "test_mod/facade_panel");

                // ...and clearing the owned registry through IGameRegistry is
                // observed by the facade: the facade and the owned instance
                // are one and the same object.
                ((IGameRegistry)services.Menus).Clear();
                Assert.Empty(MenuRegistry.AllButtons);
                Assert.Empty(MenuRegistry.AllPanels);
                Assert.Null(MenuRegistry.GetButtonById("test_mod/facade_button"));
                Assert.Null(MenuRegistry.GetPanelById("test_mod/facade_panel"));
            }
            finally
            {
                ((IGameRegistry)services.Menus).Clear();
                AppServices.SetCurrent(saved);
            }
        }
    }
}
